using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.ConfigCenter.Abstractions;

namespace JZVerse.MicroHuaxia.ConfigCenter.Core.Caching;

/// <summary>
/// 内存配置缓存实现
/// </summary>
public class InMemoryConfigCache(bool enabled = true, int ttlSeconds = 60) : IConfigCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly TimeSpan _defaultTtl = TimeSpan.FromSeconds(ttlSeconds);

    public bool IsCacheEnabled => enabled;

    public Task<Dictionary<string, string>?> GetAsync(
        string cacheKey,
        CancellationToken cancellationToken = default)
    {
        if (!enabled)
            return Task.FromResult<Dictionary<string, string>?>(null);

        if (_cache.TryGetValue(cacheKey, out var entry))
        {
            if (entry.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return Task.FromResult<Dictionary<string, string>?>(entry.Config);
            }

            // 已过期，移除
            _cache.TryRemove(cacheKey, out _);
        }

        return Task.FromResult<Dictionary<string, string>?>(null);
    }

    public Task SetAsync(
        string cacheKey,
        Dictionary<string, string> config,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default)
    {
        if (!enabled)
            return Task.CompletedTask;

        var entry = new CacheEntry
        {
            Config = new Dictionary<string, string>(config),
            ExpiresAt = DateTimeOffset.UtcNow.Add(ttl ?? _defaultTtl),
        };

        _cache[cacheKey] = entry;
        return Task.CompletedTask;
    }

    public Task InvalidateAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        _cache.TryRemove(cacheKey, out _);
        return Task.CompletedTask;
    }

    public Task InvalidateByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var keysToRemove = _cache.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        foreach (var key in keysToRemove)
        {
            _cache.TryRemove(key, out _);
        }
        return Task.CompletedTask;
    }

    private sealed class CacheEntry
    {
        public required Dictionary<string, string> Config { get; init; }
        public DateTimeOffset ExpiresAt { get; init; }
    }
}

/// <summary>
/// 空缓存实现（禁用缓存时使用）
/// </summary>
public class NullConfigCache : IConfigCache
{
    public bool IsCacheEnabled => false;

    public Task<Dictionary<string, string>?> GetAsync(
        string cacheKey,
        CancellationToken cancellationToken = default)
        => Task.FromResult<Dictionary<string, string>?>(null);

    public Task SetAsync(
        string cacheKey,
        Dictionary<string, string> config,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task InvalidateAsync(string cacheKey, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task InvalidateByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
