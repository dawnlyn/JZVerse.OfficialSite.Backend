using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.ServiceCommunication.Core.Resilience;

/// <summary>
/// 熔断器实现
/// </summary>
public sealed class CircuitBreaker : ICircuitBreaker
{
    private readonly CircuitBreakerOptions _options;
    private readonly ILogger<CircuitBreaker> _logger;
    private readonly Lock _lock = new();

    private CircuitBreakerState _state = CircuitBreakerState.Closed;
    private int _failureCount;
    private int _successCount;
    private int _requestCount;
    private DateTimeOffset _lastStateChange = DateTimeOffset.UtcNow;
    private DateTimeOffset _openedAt;

    public CircuitBreaker(
        string name,
        CircuitBreakerOptions options,
        ILogger<CircuitBreaker> logger)
    {
        Name = name;
        _options = options;
        _logger = logger;
    }

    public string Name { get; }

    public CircuitBreakerState State
    {
        get
        {
            lock (_lock)
            {
                if (_state == CircuitBreakerState.Open && ShouldAttemptReset())
                {
                    TransitionTo(CircuitBreakerState.HalfOpen);
                }
                return _state;
            }
        }
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        EnsureNotOpen();

        try
        {
            var result = await action(cancellationToken);
            OnSuccess();
            return result;
        }
        catch (Exception ex) when (IsHandledException(ex))
        {
            OnFailure();
            throw;
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

    public void Open()
    {
        lock (_lock)
        {
            TransitionTo(CircuitBreakerState.Open);
        }
    }

    public void Close()
    {
        lock (_lock)
        {
            TransitionTo(CircuitBreakerState.Closed);
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _failureCount = 0;
            _successCount = 0;
            _requestCount = 0;
            TransitionTo(CircuitBreakerState.Closed);
        }
    }

    private void EnsureNotOpen()
    {
        var currentState = State; // 触发半开检查

        if (currentState == CircuitBreakerState.Open)
        {
            throw new BrokenCircuitException(
                $"Circuit breaker '{Name}' is open. Retry after {_options.BreakDuration.TotalSeconds} seconds.");
        }
    }

    private void OnSuccess()
    {
        lock (_lock)
        {
            _requestCount++;

            if (_state == CircuitBreakerState.HalfOpen)
            {
                _successCount++;
                if (_successCount >= _options.SuccessThreshold)
                {
                    _logger.LogInformation(
                        "Circuit breaker '{Name}' recovered after {SuccessCount} successful requests",
                        Name, _successCount);
                    TransitionTo(CircuitBreakerState.Closed);
                }
            }
            else if (_state == CircuitBreakerState.Closed)
            {
                // 重置失败计数（滑动窗口内的成功请求）
                if (DateTimeOffset.UtcNow - _lastStateChange > _options.SamplingDuration)
                {
                    _failureCount = 0;
                    _requestCount = 1;
                    _lastStateChange = DateTimeOffset.UtcNow;
                }
            }
        }
    }

    private void OnFailure()
    {
        lock (_lock)
        {
            _requestCount++;
            _failureCount++;

            if (_state == CircuitBreakerState.HalfOpen)
            {
                _logger.LogWarning(
                    "Circuit breaker '{Name}' failed during half-open state, reopening",
                    Name);
                TransitionTo(CircuitBreakerState.Open);
                return;
            }

            if (_state == CircuitBreakerState.Closed)
            {
                // 检查是否需要打开熔断器
                if (_requestCount >= _options.MinimumThroughput)
                {
                    var failureRate = (double)_failureCount / _requestCount;
                    if (failureRate >= _options.FailureRateThreshold ||
                        _failureCount >= _options.FailureThreshold)
                    {
                        _logger.LogWarning(
                            "Circuit breaker '{Name}' opened. Failures: {Failures}/{Requests} ({FailureRate:P})",
                            Name, _failureCount, _requestCount, failureRate);
                        TransitionTo(CircuitBreakerState.Open);
                    }
                }
            }
        }
    }

    private bool ShouldAttemptReset()
    {
        return DateTimeOffset.UtcNow - _openedAt >= _options.BreakDuration;
    }

    private void TransitionTo(CircuitBreakerState newState)
    {
        if (_state == newState) return;

        var oldState = _state;
        _state = newState;
        _lastStateChange = DateTimeOffset.UtcNow;

        if (newState == CircuitBreakerState.Open)
        {
            _openedAt = DateTimeOffset.UtcNow;
        }
        else if (newState == CircuitBreakerState.HalfOpen)
        {
            _successCount = 0;
        }
        else if (newState == CircuitBreakerState.Closed)
        {
            _failureCount = 0;
            _successCount = 0;
            _requestCount = 0;
        }

        _logger.LogInformation(
            "Circuit breaker '{Name}' transitioned from {OldState} to {NewState}",
            Name, oldState, newState);
    }

    private bool IsHandledException(Exception ex)
    {
        return _options.HandledExceptions.Any(t => t.IsInstanceOfType(ex));
    }
}

/// <summary>
/// 熔断器打开异常
/// </summary>
public sealed class BrokenCircuitException(string message) : Exception(message);
