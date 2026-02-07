using JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;
using JZVerse.MicroHuaxia.Gateway.Resilience.Abstractions;
using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.Resilience;
using JZVerse.MicroHuaxia.ServiceCommunication.Core.Resilience;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Gateway.Resilience.Pipeline;

/// <summary>
/// Gateway 弹性管道实现
/// 执行顺序：舱壁 → 重试 → 熔断 → 超时 → 降级
/// </summary>
public sealed class GatewayResiliencePipeline : IGatewayResiliencePipeline
{
    private readonly GatewayRoute _route;
    private readonly IBulkhead? _bulkhead;
    private readonly ICircuitBreaker? _circuitBreaker;
    private readonly IFallbackHandlerRegistry _fallbackRegistry;
    private readonly ILogger<GatewayResiliencePipeline> _logger;
    private readonly TimeSpan _timeout;
    private readonly RouteRetry? _retryConfig;

    private static readonly Random Jitter = new();

    public GatewayResiliencePipeline(
        GatewayRoute route,
        IBulkhead? bulkhead,
        ICircuitBreaker? circuitBreaker,
        IFallbackHandlerRegistry fallbackRegistry,
        ILogger<GatewayResiliencePipeline> logger)
    {
        _route = route;
        _bulkhead = bulkhead;
        _circuitBreaker = circuitBreaker;
        _fallbackRegistry = fallbackRegistry;
        _logger = logger;
        _timeout = route.Timeout ?? TimeSpan.FromSeconds(30);
        _retryConfig = route.Retry;
    }

    public async Task<T> ExecuteAsync<T>(
        IGatewayResilienceContext context,
        Func<IGatewayResilienceContext, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. 舱壁检查
            if (_bulkhead != null && _route.Bulkhead?.Enabled == true)
            {
                return await _bulkhead.ExecuteAsync(async ct =>
                {
                    return await ExecuteWithResilienceAsync(context, action, ct);
                }, cancellationToken);
            }
            else
            {
                return await ExecuteWithResilienceAsync(context, action, cancellationToken);
            }
        }
        catch (BulkheadRejectedException ex)
        {
            context.WasBulkheadRejected = true;
            context.LastException = ex;
            _logger.LogWarning(ex, "Request rejected by bulkhead for route '{RouteId}'", _route.RouteId);

            // 尝试降级
            return await TryFallbackAsync<T>(context, ex, cancellationToken);
        }
    }

    private async Task<T> ExecuteWithResilienceAsync<T>(
        IGatewayResilienceContext context,
        Func<IGatewayResilienceContext, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        // 2. 熔断器检查
        if (_circuitBreaker != null && _route.CircuitBreaker?.Enabled == true)
        {
            if (_circuitBreaker.State == CircuitBreakerState.Open)
            {
                context.WasCircuitBroken = true;
                _logger.LogWarning(
                    "Circuit breaker is open for route '{RouteId}', attempting fallback",
                    _route.RouteId);

                return await TryFallbackAsync<T>(context,
                    new BrokenCircuitException($"Circuit breaker is open for route '{_route.RouteId}'"),
                    cancellationToken);
            }
        }

        // 3. 重试循环
        var maxAttempts = (_retryConfig?.Enabled == true ? _retryConfig.MaxRetries : 0) + 1;
        var delay = _retryConfig?.InitialDelay ?? TimeSpan.FromMilliseconds(100);
        Exception? lastException = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            context.AttemptCount = attempt;

            try
            {
                // 4. 超时控制
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(_timeout);

                T result;

                // 5. 使用熔断器执行（如果启用）
                if (_circuitBreaker != null && _route.CircuitBreaker?.Enabled == true)
                {
                    result = await _circuitBreaker.ExecuteAsync(
                        async ct => await action(context, ct),
                        timeoutCts.Token);
                }
                else
                {
                    result = await action(context, timeoutCts.Token);
                }

                // 成功，记录日志
                if (attempt > 1)
                {
                    _logger.LogInformation(
                        "Request succeeded on attempt {Attempt} for route '{RouteId}'",
                        attempt, _route.RouteId);
                }

                return result;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // 超时
                lastException = new TimeoutException(
                    $"Request timed out after {_timeout.TotalMilliseconds}ms for route '{_route.RouteId}'");
                _logger.LogWarning(lastException, "Request timeout on attempt {Attempt} for route '{RouteId}'",
                    attempt, _route.RouteId);
            }
            catch (BrokenCircuitException ex)
            {
                context.WasCircuitBroken = true;
                lastException = ex;
                _logger.LogWarning(ex, "Circuit breaker opened during attempt {Attempt} for route '{RouteId}'",
                    attempt, _route.RouteId);
                break; // 熔断后不再重试
            }
            catch (HttpRequestException ex) when (ShouldRetry(ex, attempt, maxAttempts))
            {
                lastException = ex;
                _logger.LogWarning(ex, "Request failed on attempt {Attempt}/{MaxAttempts} for route '{RouteId}'",
                    attempt, maxAttempts, _route.RouteId);
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogError(ex, "Unexpected error on attempt {Attempt} for route '{RouteId}'",
                    attempt, _route.RouteId);
                break; // 非预期异常不重试
            }

            // 计算下次重试延迟
            if (attempt < maxAttempts && _retryConfig?.Enabled == true)
            {
                var delayWithJitter = GetDelayWithJitter(delay);
                _logger.LogDebug(
                    "Waiting {Delay}ms before retry attempt {NextAttempt} for route '{RouteId}'",
                    delayWithJitter.TotalMilliseconds, attempt + 1, _route.RouteId);

                await Task.Delay(delayWithJitter, cancellationToken);
                delay = GetNextDelay(delay);
            }
        }

        // 所有重试都失败，尝试降级
        context.LastException = lastException;
        return await TryFallbackAsync<T>(context, lastException!, cancellationToken);
    }

    private bool ShouldRetry(HttpRequestException ex, int attempt, int maxAttempts)
    {
        if (attempt >= maxAttempts)
        {
            return false;
        }

        if (_retryConfig?.Enabled != true)
        {
            return false;
        }

        // 检查状态码是否可重试
        if (ex.StatusCode.HasValue)
        {
            return _retryConfig.RetryableStatusCodes.Contains((int)ex.StatusCode.Value);
        }

        // 连接失败等网络错误通常是可重试的
        return true;
    }

    private TimeSpan GetDelayWithJitter(TimeSpan delay)
    {
        // 添加 ±25% 的抖动
        var jitterFactor = 0.75 + (Jitter.NextDouble() * 0.5);
        return TimeSpan.FromMilliseconds(delay.TotalMilliseconds * jitterFactor);
    }

    private TimeSpan GetNextDelay(TimeSpan currentDelay)
    {
        var multiplier = _retryConfig?.BackoffMultiplier ?? 2.0;
        var maxDelay = _retryConfig?.MaxDelay ?? TimeSpan.FromSeconds(30);

        var next = TimeSpan.FromMilliseconds(currentDelay.TotalMilliseconds * multiplier);
        return next > maxDelay ? maxDelay : next;
    }

    private async Task<T> TryFallbackAsync<T>(
        IGatewayResilienceContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var handler = _fallbackRegistry.SelectHandler(context, exception);

        if (handler != null)
        {
            _logger.LogInformation(
                "Executing fallback handler '{HandlerName}' for route '{RouteId}'",
                handler.Name, _route.RouteId);

            var fallbackResult = await handler.HandleAsync(context, exception, cancellationToken);

            context.FallbackExecuted = true;
            context.FallbackType = handler.Name;

            // 如果 T 是 FallbackResult，直接返回
            if (fallbackResult is T result)
            {
                return result;
            }

            // 否则抛出包含降级结果的异常，让调用者处理
            throw new FallbackExecutedException(fallbackResult, exception);
        }

        // 没有可用的降级处理器，重新抛出原始异常
        _logger.LogError(exception, "No fallback handler available for route '{RouteId}'", _route.RouteId);
        throw exception;
    }

    public async Task ExecuteAsync(
        IGatewayResilienceContext context,
        Func<IGatewayResilienceContext, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(context, async (ctx, ct) =>
        {
            await action(ctx, ct);
            return true;
        }, cancellationToken);
    }
}

/// <summary>
/// 降级执行异常
/// 当降级处理器执行成功但类型不匹配时抛出，包含降级结果
/// </summary>
public sealed class FallbackExecutedException : Exception
{
    public FallbackExecutedException(FallbackResult result, Exception innerException)
        : base("Fallback executed successfully", innerException)
    {
        Result = result;
    }

    public FallbackResult Result { get; }
}
