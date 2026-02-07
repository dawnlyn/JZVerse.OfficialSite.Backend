using JZVerse.MicroHuaxia.Gateway.Abstractions.RateLimiting;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Gateway.Core.RateLimiting;

/// <summary>
/// 漏桶限流器实现
/// 特点：以固定速率处理请求，平滑输出流量
/// </summary>
/// <remarks>
/// 算法原理：
/// - 请求到达时进入队列（桶）
/// - 桶以固定速率（LeakRatePerSecond）漏出请求进行处理
/// - 桶满时拒绝新请求
/// - 适合需要平滑流量输出的场景
/// </remarks>
public sealed class LeakyBucketRateLimiter : IRateLimiter
{
    private readonly ILogger<LeakyBucketRateLimiter> _logger;
    private readonly IRateLimiterStore _store;

    public LeakyBucketRateLimiter(
        ILogger<LeakyBucketRateLimiter> logger,
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
        // 漏桶使用 Limit 作为桶容量，TokensPerSecond 作为漏出速率
        var leakRate = policy.TokensPerSecond > 0
            ? policy.TokensPerSecond
            : (double)policy.Limit / policy.Window.TotalSeconds;

        var state = await _store.EnqueueRequestAsync(
            key,
            leakRate,
            policy.BucketCapacity,
            cancellationToken);

        if (state.IsAllowed)
        {
            // 计算重置时间（队列清空的时间）
            var secondsToEmpty = state.QueueLength / leakRate;
            var resetAt = DateTimeOffset.UtcNow.AddSeconds(secondsToEmpty);

            return RateLimitResult.Allowed(
                state.QueueLength,
                state.BucketCapacity,
                resetAt);
        }

        _logger.LogWarning(
            "漏桶限流: {Key}, 队列长度: {QueueLength}, 桶容量: {Capacity}",
            key, state.QueueLength, state.BucketCapacity);

        return RateLimitResult.Limited(
            state.QueueLength,
            state.BucketCapacity,
            state.RetryAfter ?? TimeSpan.FromSeconds(1.0 / leakRate),
            DateTimeOffset.UtcNow.Add(state.RetryAfter ?? TimeSpan.FromSeconds(1)));
    }
}
