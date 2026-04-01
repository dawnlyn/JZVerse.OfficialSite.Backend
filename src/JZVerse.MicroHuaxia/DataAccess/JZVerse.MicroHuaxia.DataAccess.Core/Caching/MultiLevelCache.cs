using JZVerse.MicroHuaxia.DataAccess.Abstractions.Caching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.DataAccess.Core.Caching;

/// <summary>
/// 多级缓存协调器实现
/// </summary>
/// <remarks>
/// 读取流程：L1(内存) → L2(Redis) → factory(数据库)
/// 写入流程：同时写入启用的所有级别
/// 失效流程：同时清除所有级别
/// </remarks>
public sealed class MultiLevelCache : IMultiLevelCache
{
    private readonly ICacheProvider _memoryCache;
    private readonly ICacheProvider _redisCache;
    private readonly ICacheKeyGenerator _keyGenerator;
    private readonly CacheOptions _options;
    private readonly ILogger<MultiLevelCache> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public MultiLevelCache(
        IEnumerable<ICacheProvider> providers,
        ICacheKeyGenerator keyGenerator,
        IOptions<CacheOptions> options,
        ILogger<MultiLevelCache> logger)
    {
        var providerList = providers.ToList();
        _memoryCache = providerList.FirstOrDefault(p => p.Level == CacheLevel.Memory)
            ?? throw new InvalidOperationException("Memory cache provider not found");
        _redisCache = providerList.FirstOrDefault(p => p.Level == CacheLevel.Redis)
            ?? throw new InvalidOperationException("Redis cache provider not found");

        _keyGenerator = keyGenerator;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<T?> GetOrAddAsync<T>(
        string key,
        Func<Task<T?>> factory,
        CacheStrategyOptions? strategy = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return await factory();
        }

        strategy ??= _options.DefaultStrategy;

        // 1. 尝试从 L1(内存) 读取
        if (strategy.Levels.HasFlag(CacheLevel.Memory) && _memoryCache.IsAvailable)
        {
            var cached = await _memoryCache.GetAsync<CacheWrapper<T>>(key, cancellationToken);

            if (cached is not null)
            {
                if (cached.IsNullMarker)
                {
                    _logger.LogDebug("Cache hit (null marker) [L1]: {Key}", key);
                    return default;
                }

                _logger.LogDebug("Cache hit [L1]: {Key}", key);
                return cached.Value;
            }
        }

        // 2. 尝试从 L2(Redis) 读取
        if (strategy.Levels.HasFlag(CacheLevel.Redis) && _redisCache.IsAvailable)
        {
            var cached = await _redisCache.GetAsync<CacheWrapper<T>>(key, cancellationToken);

            if (cached is not null)
            {
                if (cached.IsNullMarker)
                {
                    _logger.LogDebug("Cache hit (null marker) [L2]: {Key}", key);
                    // 回填 L1
                    if (strategy.Levels.HasFlag(CacheLevel.Memory) && _memoryCache.IsAvailable)
                    {
                        await _memoryCache.SetAsync(key, cached, strategy.GetNullValueExpiration(), cancellationToken);
                    }
                    return default;
                }

                _logger.LogDebug("Cache hit [L2]: {Key}", key);

                // 回填 L1
                if (strategy.Levels.HasFlag(CacheLevel.Memory) && _memoryCache.IsAvailable)
                {
                    await _memoryCache.SetAsync(key, cached, strategy.GetExpiration(), cancellationToken);
                }

                return cached.Value;
            }
        }

        // 3. 缓存未命中，使用互斥锁防止缓存击穿
        await _lock.WaitAsync(cancellationToken);
        try
        {
            // 双重检查
            if (strategy.Levels.HasFlag(CacheLevel.Memory) && _memoryCache.IsAvailable)
            {
                var cached = await _memoryCache.GetAsync<CacheWrapper<T>>(key, cancellationToken);
                if (cached is not null)
                {
                    return cached.IsNullMarker ? default : cached.Value;
                }
            }

            // 从数据库加载
            _logger.LogDebug("Cache miss, loading from database: {Key}", key);
            var value = await factory();

            // 写入缓存
            if (value is not null)
            {
                var wrapper = new CacheWrapper<T> { Value = value, IsNullMarker = false };
                await SetInternalAsync(key, wrapper, strategy, cancellationToken);
            }
            else if (strategy.CacheNullValue)
            {
                // 缓存空值（防止缓存穿透）
                var wrapper = new CacheWrapper<T> { Value = default, IsNullMarker = true };
                var nullExpiration = strategy.GetNullValueExpiration();
                var nullStrategy = new CacheStrategyOptions
                {
                    Levels = strategy.Levels,
                    ExpirationSeconds = (int)nullExpiration.TotalSeconds,
                    CacheNullValue = strategy.CacheNullValue
                };
                await SetInternalAsync(key, wrapper, nullStrategy, cancellationToken);
            }

            return value;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(
        string key,
        T value,
        CacheStrategyOptions? strategy = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return;

        strategy ??= _options.DefaultStrategy;
        var wrapper = new CacheWrapper<T> { Value = value, IsNullMarker = false };
        await SetInternalAsync(key, wrapper, strategy, cancellationToken);
    }

    private async Task SetInternalAsync<T>(
        string key,
        CacheWrapper<T> wrapper,
        CacheStrategyOptions strategy,
        CancellationToken cancellationToken)
    {
        var expiration = strategy.GetExpiration();
        var tasks = new List<Task>();

        // 并行写入所有启用的缓存级别
        if (strategy.Levels.HasFlag(CacheLevel.Memory) && _memoryCache.IsAvailable)
        {
            tasks.Add(_memoryCache.SetAsync(key, wrapper, expiration, cancellationToken));
        }

        if (strategy.Levels.HasFlag(CacheLevel.Redis) && _redisCache.IsAvailable)
        {
            tasks.Add(_redisCache.SetAsync(key, wrapper, expiration, cancellationToken));
        }

        await Task.WhenAll(tasks);
    }

    /// <inheritdoc />
    public async Task InvalidateAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return;

        var tasks = new List<Task>
        {
            _memoryCache.RemoveAsync(key, cancellationToken),
            _redisCache.RemoveAsync(key, cancellationToken)
        };

        await Task.WhenAll(tasks);
        _logger.LogDebug("Cache invalidated: {Key}", key);
    }

    /// <inheritdoc />
    public async Task InvalidateByEntityAsync<TEntity>(object primaryKey, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return;

        var key = _keyGenerator.GenerateKey<TEntity>(primaryKey);
        await InvalidateAsync(key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task InvalidateByEntityTypeAsync<TEntity>(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return;

        var prefix = _keyGenerator.GenerateEntityPrefix<TEntity>();

        var tasks = new List<Task>
        {
            _memoryCache.RemoveByPrefixAsync(prefix, cancellationToken),
            _redisCache.RemoveByPrefixAsync(prefix, cancellationToken)
        };

        await Task.WhenAll(tasks);
        _logger.LogDebug("Cache invalidated by entity type: {EntityType}", typeof(TEntity).Name);
    }

    /// <inheritdoc />
    public async Task<IDictionary<string, T?>> GetManyAsync<T>(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, T?>();

        foreach (var key in keys)
        {
            var value = await GetOrAddAsync<T>(key, () => Task.FromResult<T?>(default), null, cancellationToken);
            result[key] = value;
        }

        return result;
    }

    /// <inheritdoc />
    public async Task SetManyAsync<T>(
        IDictionary<string, T> items,
        CacheStrategyOptions? strategy = null,
        CancellationToken cancellationToken = default)
    {
        var tasks = items.Select(kvp => SetAsync(kvp.Key, kvp.Value, strategy, cancellationToken));
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 缓存包装器（用于区分空值和未缓存）
    /// </summary>
    private sealed class CacheWrapper<T>
    {
        public T? Value { get; init; }
        public bool IsNullMarker { get; init; }
    }
}
