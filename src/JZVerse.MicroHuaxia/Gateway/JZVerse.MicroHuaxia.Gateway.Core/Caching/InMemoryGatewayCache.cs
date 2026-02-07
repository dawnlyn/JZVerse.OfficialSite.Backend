using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Caching;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Gateway.Core.Caching;

/// <summary>
/// 内存网关缓存实现
/// </summary>
public sealed class InMemoryGatewayCache : IGatewayCache
{
    private readonly ConcurrentDictionary<string, CachedEntry> _cache = new();
    private readonly ILogger<InMemoryGatewayCache> _logger;
    private readonly Timer _cleanupTimer;

    public InMemoryGatewayCache(ILogger<InMemoryGatewayCache> logger)
    {
        _logger = logger;
        // 每分钟清理过期条目
        _cleanupTimer = new Timer(Cleanup, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public ValueTask<CachedResponse?> TryGetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt > DateTimeOffset.UtcNow)
            {
                _logger.LogDebug("缓存命中: {Key}", key);
                return ValueTask.FromResult<CachedResponse?>(entry.Response);
            }

            // 过期，移除
            _cache.TryRemove(key, out _);
        }

        return ValueTask.FromResult<CachedResponse?>(null);
    }

    public ValueTask SetAsync(string key, CachedResponse response, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(ttl);
        var entry = new CachedEntry
        {
            Response = response with { ExpiresAt = expiresAt },
            ExpiresAt = expiresAt
        };

        _cache[key] = entry;
        _logger.LogDebug("缓存设置: {Key}, TTL: {Ttl}", key, ttl);

        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _cache.TryRemove(key, out _);
        return ValueTask.CompletedTask;
    }

    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        _cache.Clear();
        _logger.LogInformation("缓存已清空");
        return ValueTask.CompletedTask;
    }

    private void Cleanup(object? state)
    {
        var now = DateTimeOffset.UtcNow;
        var keysToRemove = _cache
            .Where(kvp => kvp.Value.ExpiresAt <= now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _cache.TryRemove(key, out _);
        }

        if (keysToRemove.Count > 0)
        {
            _logger.LogDebug("清理过期缓存条目: {Count}", keysToRemove.Count);
        }
    }

    private sealed record CachedEntry
    {
        public required CachedResponse Response { get; init; }
        public DateTimeOffset ExpiresAt { get; init; }
    }
}

/// <summary>
/// 缓存键生成器实现
/// </summary>
public sealed class CacheKeyGenerator : ICacheKeyGenerator
{
    public string GenerateKey(HttpContext context, RouteCache cacheConfig)
    {
        var request = context.Request;
        var parts = new List<string>
        {
            request.Method,
            request.Path.Value ?? "/"
        };

        // 添加 Query 参数变化
        if (cacheConfig.VaryByQuery.Count > 0)
        {
            foreach (var queryName in cacheConfig.VaryByQuery.OrderBy(q => q))
            {
                if (request.Query.TryGetValue(queryName, out var queryValue))
                {
                    parts.Add($"q:{queryName}={queryValue}");
                }
            }
        }
        else
        {
            // 默认包含所有 Query 参数
            foreach (var (key, value) in request.Query.OrderBy(q => q.Key))
            {
                parts.Add($"q:{key}={value}");
            }
        }

        // 添加 Header 变化
        foreach (var headerName in cacheConfig.VaryByHeader.OrderBy(h => h))
        {
            if (request.Headers.TryGetValue(headerName, out var headerValue))
            {
                parts.Add($"h:{headerName}={headerValue}");
            }
        }

        var keyString = string.Join("|", parts);
        return $"cache:{ComputeHash(keyString)}";
    }

    private static string ComputeHash(string input)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
