using JZVerse.MicroHuaxia.DataAccess.Abstractions.Caching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace JZVerse.MicroHuaxia.DataAccess.Core.Caching.Providers;

/// <summary>
/// L2 Redis/Garnet 缓存提供者
/// </summary>
public sealed class RedisCacheProvider : ICacheProvider, IDisposable
{
    private readonly ICacheSerializer _serializer;
    private readonly ILogger<RedisCacheProvider> _logger;
    private readonly CacheOptions _options;
    private readonly Lazy<IConnectionMultiplexer> _connection;
    private bool _disposed;

    public RedisCacheProvider(
        ICacheSerializer serializer,
        IOptions<CacheOptions> options,
        ILogger<RedisCacheProvider> logger)
    {
        _serializer = serializer;
        _logger = logger;
        _options = options.Value;

        _connection = new Lazy<IConnectionMultiplexer>(() =>
        {
            var configOptions = ConfigurationOptions.Parse(_options.Redis.ConnectionString);
            configOptions.ConnectTimeout = _options.Redis.ConnectTimeoutMs;
            configOptions.SyncTimeout = _options.Redis.SyncTimeoutMs;
            configOptions.AllowAdmin = _options.Redis.AllowAdmin;
            configOptions.AbortOnConnectFail = false;

            var connection = ConnectionMultiplexer.Connect(configOptions);

            connection.ConnectionFailed += (_, e) =>
            {
                _logger.LogWarning("Redis connection failed: {EndPoint}, {FailureType}", e.EndPoint, e.FailureType);
            };

            connection.ConnectionRestored += (_, e) =>
            {
                _logger.LogInformation("Redis connection restored: {EndPoint}", e.EndPoint);
            };

            return connection;
        });
    }

    /// <inheritdoc />
    public CacheLevel Level => CacheLevel.Redis;

    /// <inheritdoc />
    public bool IsAvailable
    {
        get
        {
            if (!_options.Redis.Enabled || _disposed)
                return false;

            try
            {
                return _connection.Value.IsConnected;
            }
            catch
            {
                return false;
            }
        }
    }

    private IDatabase Database => _connection.Value.GetDatabase(_options.Redis.Database);

    private string GetFullKey(string key) => $"{_options.Redis.InstanceName}{key}";

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            return default;

        try
        {
            var fullKey = GetFullKey(key);
            var value = await Database.StringGetAsync(fullKey);

            if (value.IsNullOrEmpty)
            {
                _logger.LogDebug("Cache miss [Redis]: {Key}", key);
                return default;
            }

            _logger.LogDebug("Cache hit [Redis]: {Key}", key);
            return _serializer.DeserializeFromString<T>(value!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache [Redis]: {Key}", key);

            if (_options.Redis.FallbackOnFailure)
                return default;

            throw;
        }
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            return;

        try
        {
            var fullKey = GetFullKey(key);
            var serialized = _serializer.SerializeToString(value);

            var exp = expiration ?? (_options.Redis.DefaultExpirationSeconds > 0
                ? TimeSpan.FromSeconds(_options.Redis.DefaultExpirationSeconds)
                : (TimeSpan?)null);

            await Database.StringSetAsync(fullKey, serialized, exp);

            _logger.LogDebug("Cache set [Redis]: {Key}, Expiration: {Expiration}", key, exp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache [Redis]: {Key}", key);

            if (!_options.Redis.FallbackOnFailure)
                throw;
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            return;

        try
        {
            var fullKey = GetFullKey(key);
            await Database.KeyDeleteAsync(fullKey);
            _logger.LogDebug("Cache removed [Redis]: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache [Redis]: {Key}", key);

            if (!_options.Redis.FallbackOnFailure)
                throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            return false;

        try
        {
            var fullKey = GetFullKey(key);
            return await Database.KeyExistsAsync(fullKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking cache exists [Redis]: {Key}", key);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            return;

        try
        {
            var fullPrefix = GetFullKey(prefix);
            var server = GetServer();

            if (server is null)
            {
                _logger.LogWarning("No Redis server available for SCAN operation");
                return;
            }

            var keys = server.Keys(
                database: _options.Redis.Database,
                pattern: $"{fullPrefix}*",
                pageSize: 1000);

            var keyArray = keys.ToArray();

            if (keyArray.Length > 0)
            {
                await Database.KeyDeleteAsync(keyArray);
            }

            _logger.LogDebug("Cache removed by prefix [Redis]: {Prefix}, Count: {Count}", prefix, keyArray.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache by prefix [Redis]: {Prefix}", prefix);

            if (!_options.Redis.FallbackOnFailure)
                throw;
        }
    }

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            return;

        try
        {
            // 只清除当前应用的缓存
            await RemoveByPrefixAsync(string.Empty, cancellationToken);
            _logger.LogDebug("Cache cleared [Redis]");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache [Redis]");

            if (!_options.Redis.FallbackOnFailure)
                throw;
        }
    }

    private IServer? GetServer()
    {
        try
        {
            var endpoints = _connection.Value.GetEndPoints();
            return endpoints.Length > 0 ? _connection.Value.GetServer(endpoints[0]) : null;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_connection.IsValueCreated)
        {
            try
            {
                _connection.Value.Close();
                _connection.Value.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing Redis connection");
            }
        }
    }
}
