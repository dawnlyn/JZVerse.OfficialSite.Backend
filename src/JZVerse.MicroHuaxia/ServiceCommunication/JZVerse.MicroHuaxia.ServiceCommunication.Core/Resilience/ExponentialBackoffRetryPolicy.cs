using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.ServiceCommunication.Core.Resilience;

/// <summary>
/// 指数退避重试策略
/// </summary>
public sealed class ExponentialBackoffRetryPolicy(
    IOptions<RetryPolicyOptions> options,
    ILogger<ExponentialBackoffRetryPolicy> logger) : IRetryPolicy
{
    private readonly RetryPolicyOptions _options = options.Value;
    private static readonly Random Jitter = new();

    public string Name => "ExponentialBackoff";

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        var attempt = 0;
        var delay = _options.InitialDelay;

        while (true)
        {
            try
            {
                return await action(cancellationToken);
            }
            catch (Exception ex) when (ShouldRetry(ex) && attempt < _options.MaxRetries)
            {
                attempt++;
                logger.LogWarning(
                    ex,
                    "Retry attempt {Attempt}/{MaxRetries} after {Delay}ms",
                    attempt,
                    _options.MaxRetries,
                    delay.TotalMilliseconds);

                await Task.Delay(GetDelayWithJitter(delay), cancellationToken);
                delay = GetNextDelay(delay);
            }
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

    private bool ShouldRetry(Exception ex)
    {
        if (_options.RetryableExceptions.Any(t => t.IsInstanceOfType(ex)))
        {
            return true;
        }

        if (ex is HttpRequestException httpEx && httpEx.StatusCode.HasValue)
        {
            return _options.RetryableStatusCodes.Contains((int)httpEx.StatusCode.Value);
        }

        return false;
    }

    private TimeSpan GetDelayWithJitter(TimeSpan delay)
    {
        if (!_options.UseJitter)
            return delay;

        var jitterFactor = 0.5 + Jitter.NextDouble();
        return TimeSpan.FromMilliseconds(delay.TotalMilliseconds * jitterFactor);
    }

    private TimeSpan GetNextDelay(TimeSpan currentDelay)
    {
        var next = TimeSpan.FromMilliseconds(currentDelay.TotalMilliseconds * _options.BackoffMultiplier);
        return next > _options.MaxDelay ? _options.MaxDelay : next;
    }
}
