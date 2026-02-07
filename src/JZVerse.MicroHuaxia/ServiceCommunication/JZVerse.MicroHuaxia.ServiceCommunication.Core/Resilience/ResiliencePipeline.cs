using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.Resilience;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.ServiceCommunication.Core.Resilience;

/// <summary>
/// 弹性管道实现 - 组合重试、熔断、超时策略
/// </summary>
public sealed class ResiliencePipeline(
    IRetryPolicy retryPolicy,
    ICircuitBreaker circuitBreaker,
    TimeSpan timeout,
    ILogger<ResiliencePipeline> logger) : IResiliencePipeline
{
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        // 1. 检查熔断器状态
        if (circuitBreaker.State == CircuitBreakerState.Open)
        {
            throw new BrokenCircuitException(
                $"Circuit breaker '{circuitBreaker.Name}' is open.");
        }

        // 2. 创建带超时的 CancellationToken
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            // 3. 使用熔断器包装重试策略
            return await circuitBreaker.ExecuteAsync(async ct =>
            {
                // 4. 使用重试策略执行操作
                return await retryPolicy.ExecuteAsync(action, ct);
            }, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Operation timed out after {Timeout}ms", timeout.TotalMilliseconds);
            throw new TimeoutException($"Operation timed out after {timeout.TotalMilliseconds}ms");
        }
    }

    public async Task ExecuteAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(async ct =>
        {
            await action(ct);
            return true;
        }, cancellationToken);
    }
}

/// <summary>
/// 弹性管道构建器
/// </summary>
public sealed class ResiliencePipelineBuilder
{
    private IRetryPolicy? _retryPolicy;
    private ICircuitBreaker? _circuitBreaker;
    private TimeSpan _timeout = TimeSpan.FromSeconds(30);
    private ILogger<ResiliencePipeline>? _logger;

    public ResiliencePipelineBuilder WithRetryPolicy(IRetryPolicy retryPolicy)
    {
        _retryPolicy = retryPolicy;
        return this;
    }

    public ResiliencePipelineBuilder WithCircuitBreaker(ICircuitBreaker circuitBreaker)
    {
        _circuitBreaker = circuitBreaker;
        return this;
    }

    public ResiliencePipelineBuilder WithTimeout(TimeSpan timeout)
    {
        _timeout = timeout;
        return this;
    }

    public ResiliencePipelineBuilder WithLogger(ILogger<ResiliencePipeline> logger)
    {
        _logger = logger;
        return this;
    }

    public IResiliencePipeline Build()
    {
        ArgumentNullException.ThrowIfNull(_retryPolicy);
        ArgumentNullException.ThrowIfNull(_circuitBreaker);
        ArgumentNullException.ThrowIfNull(_logger);

        return new ResiliencePipeline(_retryPolicy, _circuitBreaker, _timeout, _logger);
    }
}
