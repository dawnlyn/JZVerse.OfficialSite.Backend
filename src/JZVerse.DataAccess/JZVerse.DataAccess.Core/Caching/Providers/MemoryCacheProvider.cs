using System.Collections.Concurrent;
using JZVerse.DataAccess.Abstractions.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.DataAccess.Core.Caching.Providers;

/// <summary>
/// L1 内存缓存提供者
/// </summary>
public sealed class MemoryCacheProvider : ICacheProvider, IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly ICacheSerializer _serializer;
    private readonly ILogger<MemoryCacheProvider> _logger;
    private readonly CacheOptions _options;
    private readonly ConcurrentDictionary<string, byte> _keys = new();
    private bool _disposed;

    public MemoryCacheProvider(
        IMemoryCache cache,
        ICacheSerializer serializer,
        IOptions<CacheOptions> options,
        ILogger<MemoryCacheProvider> logger)
    {
        _cache = cache;
        _serializer = serializer;
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    public CacheLevel Level => CacheLevel.Memory;

    /// <inheritdoc />
    public bool IsAvailable => _options.Memory.Enabled && !_disposed;

    /// <inheritdoc />
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            return Task.FromResult<T?>(default);

        try
        {
            if (_cache.TryGetValue(key, out var value))
            {
                if (value is byte[] bytes)
                {
                    var result = _serializer.Deserialize<T>(bytes);
                    _logger.LogDebug("Cache hit [Memory]: {Key}", key);
                    return Task.FromResult(result);
                }

                if (value is T typedValue)
                {
                    _logger.LogDebug("Cache hit [Memory]: {Key}", key);
                    return Task.FromResult<T?>(typedValue);
                }
            }

            _logger.LogDebug("Cache miss [Memory]: {Key}", key);
            return Task.FromResult<T?>(default);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache [Memory]: {Key}", key);
            return Task.FromResult<T?>(default);
        }
    }

    /// <inheritdoc />
    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            return Task.CompletedTask;

        try
        {
            var bytes = _serializer.Serialize(value);
            var options = new MemoryCacheEntryOptions();

            if (expiration.HasValue)
            {
                options.AbsoluteExpirationRelativeToNow = expiration.Value;
            }
            else if (_options.Memory.DefaultExpirationSeconds > 0)
            {
                options.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.Memory.DefaultExpirationSeconds);
            }

            // 设置大小（用于内存限制）
            options.Size = bytes.Length;

            // 注册移除回调
            options.RegisterPostEvictionCallback((k, v, reason, state) =>
            {
                _keys.TryRemove(k.ToString()!, out _);
            });

            _cache.Set(key, bytes, options);
            _keys[key] = 0;

            _logger.LogDebug("Cache set [Memory]: {Key}, Expiration: {Expiration}", key, expiration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache [Memory]: {Key}", key);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            return Task.CompletedTask;

        try
        {
            _cache.Remove(key);
            _keys.TryRemove(key, out _);
            _logger.LogDebug("Cache removed [Memory]: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache [Memory]: {Key}", key);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            return Task.FromResult(false);

        return Task.FromResult(_cache.TryGetValue(key, out _));
    }

    /// <inheritdoc />
    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            return Task.CompletedTask;

        try
        {
            var keysToRemove = _keys.Keys.Where(k => k.StartsWith(prefix)).ToList();

            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
                _keys.TryRemove(key, out _);
            }

            _logger.LogDebug("Cache removed by prefix [Memory]: {Prefix}, Count: {Count}", prefix, keysToRemove.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache by prefix [Memory]: {Prefix}", prefix);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            return Task.CompletedTask;

        try
        {
            foreach (var key in _keys.Keys)
            {
                _cache.Remove(key);
            }
            _keys.Clear();
            _logger.LogDebug("Cache cleared [Memory]");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache [Memory]");
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _keys.Clear();
    }
}
