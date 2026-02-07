using JZVerse.MicroHuaxia.Gateway.Abstractions.RateLimiting;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Gateway.Core.RateLimiting;

/// <summary>
/// 令牌桶限流器实现
/// 特点：允许突发流量，适合需要处理瞬时流量高峰的场景
/// </summary>
/// <remarks>
/// 算法原理：
/// - 以固定速率（TokensPerSecond）向桶中添加令牌
/// - 桶有最大容量（BucketCapacity），满后不再添加
/// - 每个请求需要消耗一定数量的令牌
/// - 令牌不足时拒绝请求
/// </remarks>
public sealed class TokenBucketRateLimiter : IRateLimiter
{
    private readonly ILogger<TokenBucketRateLimiter> _logger;
    private readonly IRateLimiterStore _store;

    public TokenBucketRateLimiter(
        ILogger<TokenBucketRateLimiter> logger,
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
        var state = await _store.ConsumeTokensAsync(
            key,
            policy.TokensPerSecond,
            policy.BucketCapacity,
            tokensToConsume: 1,
            cancellationToken);

        if (state.IsAllowed)
        {
            // 计算重置时间（桶满的时间）
            var tokensNeeded = policy.BucketCapacity - state.CurrentTokens;
            var secondsToFull = tokensNeeded / policy.TokensPerSecond;
            var resetAt = DateTimeOffset.UtcNow.AddSeconds(secondsToFull);

            return RateLimitResult.Allowed(
                (long)state.CurrentTokens,
                policy.BucketCapacity,
                resetAt);
        }

        _logger.LogWarning(
            "令牌桶限流: {Key}, 当前令牌: {Tokens}, 桶容量: {Capacity}",
            key, state.CurrentTokens, state.BucketCapacity);

        return RateLimitResult.Limited(
            (long)state.CurrentTokens,
            policy.BucketCapacity,
            state.RetryAfter ?? TimeSpan.FromSeconds(1.0 / policy.TokensPerSecond),
            DateTimeOffset.UtcNow.Add(state.RetryAfter ?? TimeSpan.FromSeconds(1)));
    }
}
