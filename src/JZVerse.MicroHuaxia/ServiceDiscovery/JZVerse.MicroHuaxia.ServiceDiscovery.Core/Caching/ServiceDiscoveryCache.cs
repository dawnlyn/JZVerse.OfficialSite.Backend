using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Core.Caching;

/// <summary>
/// 基于内存的服务发现缓存
/// </summary>
public class InMemoryServiceDiscoveryCache(bool _enabled = true, int ttlSeconds = 60) : IServiceDiscoveryCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly TimeSpan _ttl = TimeSpan.FromSeconds(ttlSeconds);

    /// <inheritdoc />
    public bool IsCacheEnabled => _enabled;

    /// <inheritdoc />
    public Task<IReadOnlyList<ServiceInstance>?> GetCachedInstancesAsync(
        string serviceName,
        CancellationToken cancellationToken = default
    )
    {
        if (!_enabled)
        {
            return Task.FromResult<IReadOnlyList<ServiceInstance>?>(null);
        }

        if (_cache.TryGetValue(serviceName, out var entry))
        {
            if (entry.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return Task.FromResult<IReadOnlyList<ServiceInstance>?>(entry.Instances);
            }

            // 缓存过期，移除
            _cache.TryRemove(serviceName, out _);
        }

        return Task.FromResult<IReadOnlyList<ServiceInstance>?>(null);
    }

    /// <inheritdoc />
    public Task SetCachedInstancesAsync(
        string serviceName,
        IReadOnlyList<ServiceInstance> instances,
        CancellationToken cancellationToken = default
    )
    {
        if (!_enabled)
        {
            return Task.CompletedTask;
        }

        var entry = new CacheEntry { Instances = instances, ExpiresAt = DateTimeOffset.UtcNow.Add(_ttl) };

        _cache.AddOrUpdate(serviceName, entry, (_, _) => entry);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearCacheAsync(string? serviceName = null, CancellationToken cancellationToken = default)
    {
        if (serviceName == null)
        {
            _cache.Clear();
        }
        else
        {
            _cache.TryRemove(serviceName, out _);
        }

        return Task.CompletedTask;
    }

    private sealed class CacheEntry
    {
        public required IReadOnlyList<ServiceInstance> Instances { get; init; }
        public DateTimeOffset ExpiresAt { get; init; }
    }
}

/// <summary>
/// 禁用的缓存（空实现）
/// </summary>
public class NullServiceDiscoveryCache : IServiceDiscoveryCache
{
    /// <inheritdoc />
    public bool IsCacheEnabled => false;

    /// <inheritdoc />
    public Task<IReadOnlyList<ServiceInstance>?> GetCachedInstancesAsync(
        string serviceName,
        CancellationToken cancellationToken = default
    ) => Task.FromResult<IReadOnlyList<ServiceInstance>?>(null);

    /// <inheritdoc />
    public Task SetCachedInstancesAsync(
        string serviceName,
        IReadOnlyList<ServiceInstance> instances,
        CancellationToken cancellationToken = default
    ) => Task.CompletedTask;

    /// <inheritdoc />
    public Task ClearCacheAsync(string? serviceName = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
