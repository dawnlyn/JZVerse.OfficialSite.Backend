using Microsoft.AspNetCore.Http;

namespace JZVerse.MicroHuaxia.Gateway.Abstractions.RateLimiting;

/// <summary>
/// 限流器接口
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// 尝试获取许可
    /// </summary>
    /// <param name="key">限流键</param>
    /// <param name="policy">限流策略</param>
    /// <param name="cancellationToken">取消令牌</param>
    ValueTask<RateLimitResult> TryAcquireAsync(
        string key,
        RateLimitPolicy policy,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 限流键生成器接口
/// </summary>
public interface IRateLimitKeyGenerator
{
    /// <summary>
    /// 生成限流键
    /// </summary>
    /// <param name="context">HTTP 上下文</param>
    /// <param name="routeId">路由 ID</param>
    /// <param name="keyStrategy">键策略</param>
    /// <param name="customKeyHeader">自定义键 Header 名称</param>
    string GenerateKey(
        HttpContext context,
        string routeId,
        Routing.RateLimitKeyStrategy keyStrategy,
        string? customKeyHeader = null);
}

/// <summary>
/// 限流状态存储接口
/// </summary>
public interface IRateLimiterStore
{
    /// <summary>
    /// 获取当前计数
    /// </summary>
    ValueTask<long> GetCountAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 增加计数
    /// </summary>
    /// <param name="key">限流键</param>
    /// <param name="window">时间窗口</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>增加后的计数</returns>
    ValueTask<long> IncrementAsync(string key, TimeSpan window, CancellationToken cancellationToken = default);

    /// <summary>
    /// 重置计数
    /// </summary>
    ValueTask ResetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取剩余时间
    /// </summary>
    ValueTask<TimeSpan?> GetTtlAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 消费令牌（Token Bucket）
    /// </summary>
    /// <param name="key">限流键</param>
    /// <param name="tokensPerSecond">每秒补充的令牌数</param>
    /// <param name="bucketCapacity">桶容量</param>
    /// <param name="tokensToConsume">要消费的令牌数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>令牌桶状态</returns>
    ValueTask<TokenBucketState> ConsumeTokensAsync(
        string key,
        double tokensPerSecond,
        int bucketCapacity,
        int tokensToConsume = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 入队请求（Leaky Bucket）
    /// </summary>
    /// <param name="key">限流键</param>
    /// <param name="leakRatePerSecond">每秒漏出的请求数</param>
    /// <param name="bucketCapacity">队列容量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>漏桶状态</returns>
    ValueTask<LeakyBucketState> EnqueueRequestAsync(
        string key,
        double leakRatePerSecond,
        int bucketCapacity,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 令牌桶状态
/// </summary>
public sealed record TokenBucketState
{
    /// <summary>
    /// 是否成功消费令牌
    /// </summary>
    public bool IsAllowed { get; init; }

    /// <summary>
    /// 当前剩余令牌数
    /// </summary>
    public double CurrentTokens { get; init; }

    /// <summary>
    /// 桶容量
    /// </summary>
    public int BucketCapacity { get; init; }

    /// <summary>
    /// 下次有可用令牌的等待时间
    /// </summary>
    public TimeSpan? RetryAfter { get; init; }

    /// <summary>
    /// 创建允许的状态
    /// </summary>
    public static TokenBucketState Allowed(double currentTokens, int bucketCapacity) => new()
    {
        IsAllowed = true,
        CurrentTokens = currentTokens,
        BucketCapacity = bucketCapacity
    };

    /// <summary>
    /// 创建被限流的状态
    /// </summary>
    public static TokenBucketState Limited(double currentTokens, int bucketCapacity, TimeSpan retryAfter) => new()
    {
        IsAllowed = false,
        CurrentTokens = currentTokens,
        BucketCapacity = bucketCapacity,
        RetryAfter = retryAfter
    };
}

/// <summary>
/// 漏桶状态
/// </summary>
public sealed record LeakyBucketState
{
    /// <summary>
    /// 是否允许入队
    /// </summary>
    public bool IsAllowed { get; init; }

    /// <summary>
    /// 当前队列中的请求数
    /// </summary>
    public int QueueLength { get; init; }

    /// <summary>
    /// 队列容量
    /// </summary>
    public int BucketCapacity { get; init; }

    /// <summary>
    /// 预计等待时间（队列中排队等待的时间）
    /// </summary>
    public TimeSpan? WaitTime { get; init; }

    /// <summary>
    /// 队列满时的重试等待时间
    /// </summary>
    public TimeSpan? RetryAfter { get; init; }

    /// <summary>
    /// 创建允许的状态
    /// </summary>
    public static LeakyBucketState Allowed(int queueLength, int bucketCapacity, TimeSpan waitTime) => new()
    {
        IsAllowed = true,
        QueueLength = queueLength,
        BucketCapacity = bucketCapacity,
        WaitTime = waitTime
    };

    /// <summary>
    /// 创建被限流的状态
    /// </summary>
    public static LeakyBucketState Limited(int queueLength, int bucketCapacity, TimeSpan retryAfter) => new()
    {
        IsAllowed = false,
        QueueLength = queueLength,
        BucketCapacity = bucketCapacity,
        RetryAfter = retryAfter
    };
}

/// <summary>
/// 限流器工厂接口
/// </summary>
public interface IRateLimiterFactory
{
    /// <summary>
    /// 根据算法获取或创建限流器
    /// </summary>
    /// <param name="algorithm">限流算法</param>
    IRateLimiter GetOrCreate(Routing.RateLimitAlgorithm algorithm);
}

/// <summary>
/// 限流策略
/// </summary>
public sealed record RateLimitPolicy
{
    /// <summary>
    /// 限流算法
    /// </summary>
    public Routing.RateLimitAlgorithm Algorithm { get; init; } = Routing.RateLimitAlgorithm.SlidingWindow;

    /// <summary>
    /// 时间窗口内允许的请求数
    /// </summary>
    public int Limit { get; init; } = 100;

    /// <summary>
    /// 时间窗口
    /// </summary>
    public TimeSpan Window { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// 令牌桶：每秒补充的令牌数
    /// </summary>
    public double TokensPerSecond { get; init; } = 10;

    /// <summary>
    /// 令牌桶：桶容量
    /// </summary>
    public int BucketCapacity { get; init; } = 100;
}

/// <summary>
/// 限流结果
/// </summary>
public sealed record RateLimitResult
{
    /// <summary>
    /// 是否允许请求
    /// </summary>
    public bool IsAllowed { get; init; }

    /// <summary>
    /// 当前计数
    /// </summary>
    public long CurrentCount { get; init; }

    /// <summary>
    /// 限制值
    /// </summary>
    public int Limit { get; init; }

    /// <summary>
    /// 剩余配额
    /// </summary>
    public long Remaining => Math.Max(0, Limit - CurrentCount);

    /// <summary>
    /// 重置时间
    /// </summary>
    public DateTimeOffset? ResetAt { get; init; }

    /// <summary>
    /// 重试等待时间（被限流时）
    /// </summary>
    public TimeSpan? RetryAfter { get; init; }

    /// <summary>
    /// 创建允许的结果
    /// </summary>
    public static RateLimitResult Allowed(long currentCount, int limit, DateTimeOffset? resetAt = null) => new()
    {
        IsAllowed = true,
        CurrentCount = currentCount,
        Limit = limit,
        ResetAt = resetAt
    };

    /// <summary>
    /// 创建被限流的结果
    /// </summary>
    public static RateLimitResult Limited(long currentCount, int limit, TimeSpan retryAfter, DateTimeOffset? resetAt = null) => new()
    {
        IsAllowed = false,
        CurrentCount = currentCount,
        Limit = limit,
        RetryAfter = retryAfter,
        ResetAt = resetAt
    };
}
