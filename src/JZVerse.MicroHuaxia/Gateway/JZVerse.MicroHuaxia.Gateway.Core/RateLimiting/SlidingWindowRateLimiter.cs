using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.Gateway.Abstractions.RateLimiting;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Gateway.Core.RateLimiting;

/// <summary>
/// 滑动窗口限流器实现
/// </summary>
public sealed class SlidingWindowRateLimiter : IRateLimiter
{
    private readonly ILogger<SlidingWindowRateLimiter> _logger;
    private readonly IRateLimiterStore _store;

    public SlidingWindowRateLimiter(
        ILogger<SlidingWindowRateLimiter> logger,
        IRateLimiterStore store)
    {
        _logger = logger;
        _store = store;
    }

    public async ValueTask<RateLimitResult> TryAcquireAsync(
        string key,
        RateLimitPolicy policy,
        CancellationToken cancellationToken = default)
    {
        var currentCount = await _store.IncrementAsync(key, policy.Window, cancellationToken);
        var resetAt = DateTimeOffset.UtcNow.Add(policy.Window);

        if (currentCount <= policy.Limit)
        {
            return RateLimitResult.Allowed(currentCount, policy.Limit, resetAt);
        }

        var ttl = await _store.GetTtlAsync(key, cancellationToken);
        var retryAfter = ttl ?? policy.Window;

        _logger.LogWarning("请求被限流: {Key}, 当前计数: {Count}, 限制: {Limit}",
            key, currentCount, policy.Limit);

        return RateLimitResult.Limited(currentCount, policy.Limit, retryAfter, resetAt);
    }
}

