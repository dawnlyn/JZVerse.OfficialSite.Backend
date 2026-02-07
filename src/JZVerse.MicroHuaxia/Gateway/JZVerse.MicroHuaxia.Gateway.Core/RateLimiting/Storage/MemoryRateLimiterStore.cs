using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.Gateway.Abstractions.RateLimiting;

namespace JZVerse.MicroHuaxia.Gateway.Core.RateLimiting.Storage;

/// <summary>
/// 内存限流状态存储实现
/// 支持滑动窗口、令牌桶和漏桶三种算法
/// </summary>
public sealed class MemoryRateLimiterStore : IRateLimiterStore, IDisposable
{
    private readonly ConcurrentDictionary<string, SlidingWindowEntry> _slidingWindowEntries = new();
    private readonly ConcurrentDictionary<string, TokenBucketEntry> _tokenBucketEntries = new();
    private readonly ConcurrentDictionary<string, LeakyBucketEntry> _leakyBucketEntries = new();
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    public MemoryRateLimiterStore()
    {
        // 每分钟清理过期条目
        _cleanupTimer = new Timer(Cleanup, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    #region Sliding Window

    public ValueTask<long> GetCountAsync(string key, CancellationToken cancellationToken = default)
    {
        if (_slidingWindowEntries.TryGetValue(key, out var entry) && !entry.IsExpired)
        {
            return ValueTask.FromResult(entry.Count);
        }

        return ValueTask.FromResult(0L);
    }

    public ValueTask<long> IncrementAsync(string key, TimeSpan window, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var entry = _slidingWindowEntries.AddOrUpdate(
            key,
            _ => new SlidingWindowEntry
            {
                Count = 1,
                WindowStart = now,
                ExpiresAt = now.Add(window)
            },
            (_, existing) =>
            {
                if (existing.IsExpired)
                {
                    return new SlidingWindowEntry
                    {
                        Count = 1,
                        WindowStart = now,
                        ExpiresAt = now.Add(window)
                    };
                }

                return existing with { Count = existing.Count + 1 };
            });

        return ValueTask.FromResult(entry.Count);
    }

    public ValueTask ResetAsync(string key, CancellationToken cancellationToken = default)
    {
        _slidingWindowEntries.TryRemove(key, out _);
        _tokenBucketEntries.TryRemove(key, out _);
        _leakyBucketEntries.TryRemove(key, out _);
        return ValueTask.CompletedTask;
    }

    public ValueTask<TimeSpan?> GetTtlAsync(string key, CancellationToken cancellationToken = default)
    {
        if (_slidingWindowEntries.TryGetValue(key, out var entry) && !entry.IsExpired)
        {
            return ValueTask.FromResult<TimeSpan?>(entry.ExpiresAt - DateTimeOffset.UtcNow);
        }

        return ValueTask.FromResult<TimeSpan?>(null);
    }

    #endregion

    #region Token Bucket

    public ValueTask<TokenBucketState> ConsumeTokensAsync(
        string key,
        double tokensPerSecond,
        int bucketCapacity,
        int tokensToConsume = 1,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var consumed = false;
        double availableBeforeConsume = 0;

        var entry = _tokenBucketEntries.AddOrUpdate(
            key,
            _ =>
            {
                // 新建桶，初始满令牌
                availableBeforeConsume = bucketCapacity;
                if (bucketCapacity >= tokensToConsume)
                {
                    consumed = true;
                    return new TokenBucketEntry
                    {
                        Tokens = bucketCapacity - tokensToConsume,
                        LastUpdateTime = now,
                        BucketCapacity = bucketCapacity,
                        TokensPerSecond = tokensPerSecond
                    };
                }

                // 初始容量不足（理论上不应该发生）
                return new TokenBucketEntry
                {
                    Tokens = bucketCapacity,
                    LastUpdateTime = now,
                    BucketCapacity = bucketCapacity,
                    TokensPerSecond = tokensPerSecond
                };
            },
            (_, existing) =>
            {
                // 计算经过的时间并补充令牌
                var elapsed = (now - existing.LastUpdateTime).TotalSeconds;
                var tokensToAdd = elapsed * tokensPerSecond;
                var newTokens = Math.Min(bucketCapacity, existing.Tokens + tokensToAdd);
                availableBeforeConsume = newTokens;

                // 尝试消费令牌
                if (newTokens >= tokensToConsume)
                {
                    consumed = true;
                    return existing with
                    {
                        Tokens = newTokens - tokensToConsume,
                        LastUpdateTime = now
                    };
                }

                // 令牌不足，只更新时间和令牌数（不消费）
                consumed = false;
                return existing with
                {
                    Tokens = newTokens,
                    LastUpdateTime = now
                };
            });

        if (consumed)
        {
            return ValueTask.FromResult(TokenBucketState.Allowed(
                entry.Tokens,
                bucketCapacity));
        }

        // 计算需要等待的时间
        var tokensNeeded = tokensToConsume - entry.Tokens;
        var waitSeconds = tokensNeeded / tokensPerSecond;
        var retryAfter = TimeSpan.FromSeconds(Math.Max(0.1, waitSeconds));

        return ValueTask.FromResult(TokenBucketState.Limited(
            entry.Tokens,
            bucketCapacity,
            retryAfter));
    }

    #endregion

    #region Leaky Bucket

    public ValueTask<LeakyBucketState> EnqueueRequestAsync(
        string key,
        double leakRatePerSecond,
        int bucketCapacity,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var entry = _leakyBucketEntries.AddOrUpdate(
            key,
            _ =>
            {
                // 新建漏桶，第一个请求立即处理
                return new LeakyBucketEntry
                {
                    QueueLength = 1,
                    LastLeakTime = now,
                    NextAvailableTime = now,
                    BucketCapacity = bucketCapacity,
                    LeakRatePerSecond = leakRatePerSecond
                };
            },
            (_, existing) =>
            {
                // 计算经过的时间并漏出请求
                var elapsed = (now - existing.LastLeakTime).TotalSeconds;
                var leaked = (int)(elapsed * leakRatePerSecond);
                var newQueueLength = Math.Max(0, existing.QueueLength - leaked);

                // 检查是否可以入队
                if (newQueueLength < bucketCapacity)
                {
                    // 计算下一个可用时间
                    var waitTime = TimeSpan.FromSeconds((newQueueLength + 1) / leakRatePerSecond);
                    return new LeakyBucketEntry
                    {
                        QueueLength = newQueueLength + 1,
                        LastLeakTime = now,
                        NextAvailableTime = now.Add(waitTime),
                        BucketCapacity = bucketCapacity,
                        LeakRatePerSecond = leakRatePerSecond
                    };
                }

                // 队列满，只更新漏出时间
                return existing with
                {
                    QueueLength = newQueueLength,
                    LastLeakTime = now
                };
            });

        // 判断是否成功入队
        if (entry.QueueLength <= bucketCapacity)
        {
            var waitTime = entry.NextAvailableTime - now;
            return ValueTask.FromResult(LeakyBucketState.Allowed(
                entry.QueueLength,
                bucketCapacity,
                waitTime > TimeSpan.Zero ? waitTime : TimeSpan.Zero));
        }

        // 队列满
        var retryAfter = TimeSpan.FromSeconds(1.0 / leakRatePerSecond);
        return ValueTask.FromResult(LeakyBucketState.Limited(
            entry.QueueLength,
            bucketCapacity,
            retryAfter));
    }

    #endregion

    #region Cleanup

    private void Cleanup(object? state)
    {
        if (_disposed) return;

        var now = DateTimeOffset.UtcNow;

        // 清理滑动窗口过期条目
        var slidingWindowKeysToRemove = _slidingWindowEntries
            .Where(kvp => kvp.Value.IsExpired)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in slidingWindowKeysToRemove)
        {
            _slidingWindowEntries.TryRemove(key, out _);
        }

        // 清理长时间未更新的令牌桶条目（超过 10 分钟未更新）
        var tokenBucketKeysToRemove = _tokenBucketEntries
            .Where(kvp => (now - kvp.Value.LastUpdateTime).TotalMinutes > 10)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in tokenBucketKeysToRemove)
        {
            _tokenBucketEntries.TryRemove(key, out _);
        }

        // 清理长时间未更新的漏桶条目（超过 10 分钟未更新）
        var leakyBucketKeysToRemove = _leakyBucketEntries
            .Where(kvp => (now - kvp.Value.LastLeakTime).TotalMinutes > 10)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in leakyBucketKeysToRemove)
        {
            _leakyBucketEntries.TryRemove(key, out _);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cleanupTimer.Dispose();
    }

    #endregion

    #region Entry Types

    private sealed record SlidingWindowEntry
    {
        public long Count { get; init; }
        public DateTimeOffset WindowStart { get; init; }
        public DateTimeOffset ExpiresAt { get; init; }
        public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
    }

    private sealed record TokenBucketEntry
    {
        public double Tokens { get; init; }
        public DateTimeOffset LastUpdateTime { get; init; }
        public int BucketCapacity { get; init; }
        public double TokensPerSecond { get; init; }
    }

    private sealed record LeakyBucketEntry
    {
        public int QueueLength { get; init; }
        public DateTimeOffset LastLeakTime { get; init; }
        public DateTimeOffset NextAvailableTime { get; init; }
        public int BucketCapacity { get; init; }
        public double LeakRatePerSecond { get; init; }
    }

    #endregion
}
