using System.Data;
using System.Text.RegularExpressions;
using JZVerse.MicroHuaxia.DataAccess.Abstractions;
using JZVerse.MicroHuaxia.DataAccess.Abstractions.Caching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.DataAccess.Caching.Dapper;

/// <summary>
/// 带缓存的数据库执行器（装饰器模式）
/// </summary>
/// <remarks>
/// 包装 IDbExecutor，在查询时自动检查缓存，在更新时自动失效缓存
/// </remarks>
public sealed class CachedDbExecutor : IDbExecutor
{
    private readonly IDbExecutor _inner;
    private readonly IMultiLevelCache _cache;
    private readonly ICacheKeyGenerator _keyGenerator;
    private readonly ICacheableEntityScanner _scanner;
    private readonly CacheOptions _options;
    private readonly ILogger<CachedDbExecutor> _logger;

    // 用于检测 INSERT/UPDATE/DELETE 语句的正则表达式
    private static readonly Regex InsertRegex = new(@"^\s*INSERT\s+INTO\s+(\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex UpdateRegex = new(@"^\s*UPDATE\s+(\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DeleteRegex = new(@"^\s*DELETE\s+FROM\s+(\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SelectRegex = new(@"^\s*SELECT\s+.+\s+FROM\s+(\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public CachedDbExecutor(
        IDbExecutor inner,
        IMultiLevelCache cache,
        ICacheKeyGenerator keyGenerator,
        ICacheableEntityScanner scanner,
        IOptions<CacheOptions> options,
        ILogger<CachedDbExecutor> logger)
    {
        _inner = inner;
        _cache = cache;
        _keyGenerator = keyGenerator;
        _scanner = scanner;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> QueryAsync<T>(
        string sql,
        object? param = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return await _inner.QueryAsync<T>(sql, param, cancellationToken);

        var metadata = _scanner.GetMetadata<T>();

        if (metadata is null)
        {
            // 未标记缓存，直接透传
            return await _inner.QueryAsync<T>(sql, param, cancellationToken);
        }

        // 生成查询缓存键
        var cacheKey = _keyGenerator.GenerateQueryKey<T>(sql, param);

        var strategy = CreateStrategyFromMetadata(metadata);

        var result = await _cache.GetOrAddAsync(
            cacheKey,
            async () =>
            {
                var data = await _inner.QueryAsync<T>(sql, param, cancellationToken);
                return data.ToList();
            },
            strategy,
            cancellationToken);

        return result ?? [];
    }

    /// <inheritdoc />
    public async Task<T?> QuerySingleOrDefaultAsync<T>(
        string sql,
        object? param = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return await _inner.QuerySingleOrDefaultAsync<T>(sql, param, cancellationToken);

        var metadata = _scanner.GetMetadata<T>();

        if (metadata is null)
        {
            return await _inner.QuerySingleOrDefaultAsync<T>(sql, param, cancellationToken);
        }

        // 尝试从参数中提取主键
        var primaryKey = TryExtractPrimaryKey(metadata, param);
        string cacheKey;

        if (primaryKey is not null && metadata.Attribute.Granularity == CacheGranularity.ByPrimaryKey)
        {
            // 按主键缓存
            cacheKey = _keyGenerator.GenerateKey<T>(primaryKey);
        }
        else
        {
            // 查询缓存
            cacheKey = _keyGenerator.GenerateQueryKey<T>(sql, param);
        }

        var strategy = CreateStrategyFromMetadata(metadata);

        return await _cache.GetOrAddAsync(
            cacheKey,
            () => _inner.QuerySingleOrDefaultAsync<T>(sql, param, cancellationToken),
            strategy,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T?> QueryFirstOrDefaultAsync<T>(
        string sql,
        object? param = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return await _inner.QueryFirstOrDefaultAsync<T>(sql, param, cancellationToken);

        var metadata = _scanner.GetMetadata<T>();

        if (metadata is null)
        {
            return await _inner.QueryFirstOrDefaultAsync<T>(sql, param, cancellationToken);
        }

        var primaryKey = TryExtractPrimaryKey(metadata, param);
        string cacheKey;

        if (primaryKey is not null && metadata.Attribute.Granularity == CacheGranularity.ByPrimaryKey)
        {
            cacheKey = _keyGenerator.GenerateKey<T>(primaryKey);
        }
        else
        {
            cacheKey = _keyGenerator.GenerateQueryKey<T>(sql, param);
        }

        var strategy = CreateStrategyFromMetadata(metadata);

        return await _cache.GetOrAddAsync(
            cacheKey,
            () => _inner.QueryFirstOrDefaultAsync<T>(sql, param, cancellationToken),
            strategy,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(
        string sql,
        object? param = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.ExecuteAsync(sql, param, cancellationToken);

        if (_options.Enabled && result > 0)
        {
            // 检测是否是写操作，如果是则失效相关缓存
            await TryInvalidateCacheAsync(sql, param, cancellationToken);
        }

        return result;
    }

    /// <inheritdoc />
    public Task<T?> ExecuteScalarAsync<T>(
        string sql,
        object? param = null,
        CancellationToken cancellationToken = default)
    {
        // 标量查询通常不缓存
        return _inner.ExecuteScalarAsync<T>(sql, param, cancellationToken);
    }

    /// <inheritdoc />
    public Task<T> WithTransactionAsync<T>(
        Func<IDbTransaction, Task<T>> action,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        // 事务中的操作不使用缓存
        return _inner.WithTransactionAsync(action, isolationLevel, cancellationToken);
    }

    /// <inheritdoc />
    public Task WithTransactionAsync(
        Func<IDbTransaction, Task> action,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        return _inner.WithTransactionAsync(action, isolationLevel, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IEnumerable<T>> QueryAsync<T>(
        string sql,
        object? param,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        // 事务中的查询不使用缓存
        return _inner.QueryAsync<T>(sql, param, transaction, cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(
        string sql,
        object? param,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        // 事务中的执行不使用缓存
        return _inner.ExecuteAsync(sql, param, transaction, cancellationToken);
    }

    /// <inheritdoc />
    public Task<T?> ExecuteScalarAsync<T>(
        string sql,
        object? param,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        return _inner.ExecuteScalarAsync<T>(sql, param, transaction, cancellationToken);
    }

    private CacheStrategyOptions CreateStrategyFromMetadata(CacheableEntityMetadata metadata)
    {
        return new CacheStrategyOptions
        {
            Levels = metadata.Attribute.Levels,
            ExpirationSeconds = metadata.Attribute.ExpirationSeconds > 0
                ? metadata.Attribute.ExpirationSeconds
                : _options.DefaultStrategy.ExpirationSeconds,
            CacheNullValue = metadata.Attribute.CacheNullValue
        };
    }

    private static object? TryExtractPrimaryKey(CacheableEntityMetadata metadata, object? param)
    {
        if (param is null || metadata.KeyProperties.Count == 0)
            return null;

        var paramType = param.GetType();

        // 尝试从参数中找到主键值
        if (metadata.KeyProperties.Count == 1)
        {
            var keyProp = metadata.KeyProperties[0];
            var paramProp = paramType.GetProperty(keyProp.Name)
                ?? paramType.GetProperty("Id")
                ?? paramType.GetProperty($"{metadata.EntityType.Name}Id");

            return paramProp?.GetValue(param);
        }

        // 复合主键
        var keyValues = new List<object?>();
        foreach (var keyProp in metadata.KeyProperties)
        {
            var paramProp = paramType.GetProperty(keyProp.Name);
            if (paramProp is null)
                return null;

            keyValues.Add(paramProp.GetValue(param));
        }

        return string.Join("_", keyValues.Select(v => v?.ToString() ?? string.Empty));
    }

    private async Task TryInvalidateCacheAsync(string sql, object? param, CancellationToken cancellationToken)
    {
        try
        {
            string? tableName = null;

            // 检测 INSERT
            var match = InsertRegex.Match(sql);
            if (match.Success)
            {
                tableName = match.Groups[1].Value;
            }

            // 检测 UPDATE
            if (tableName is null)
            {
                match = UpdateRegex.Match(sql);
                if (match.Success)
                {
                    tableName = match.Groups[1].Value;
                }
            }

            // 检测 DELETE
            if (tableName is null)
            {
                match = DeleteRegex.Match(sql);
                if (match.Success)
                {
                    tableName = match.Groups[1].Value;
                }
            }

            if (tableName is null)
                return;

            // 查找对应的实体类型
            var entities = _scanner.ScanEntities();
            var metadata = entities.FirstOrDefault(e =>
                string.Equals(e.TableName, tableName, StringComparison.OrdinalIgnoreCase));

            if (metadata is null)
                return;

            // 失效整个实体类型的缓存
            var prefix = _keyGenerator.GenerateEntityPrefix(metadata.EntityType);
            await _cache.InvalidateAsync(prefix);

            _logger.LogDebug("Cache invalidated for table: {Table}", tableName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate cache for SQL: {Sql}", sql);
        }
    }
}
