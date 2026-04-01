using JZVerse.MicroHuaxia.DataAccess.Abstractions;
using JZVerse.MicroHuaxia.DataAccess.Abstractions.Caching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.DataAccess.Core.Caching;

/// <summary>
/// 缓存预热服务实现
/// </summary>
public sealed class CacheWarmer : ICacheWarmer
{
    private readonly ICacheableEntityScanner _scanner;
    private readonly IMultiLevelCache _cache;
    private readonly ICacheKeyGenerator _keyGenerator;
    private readonly IDbExecutor _dbExecutor;
    private readonly CacheOptions _options;
    private readonly ILogger<CacheWarmer> _logger;

    public CacheWarmer(
        ICacheableEntityScanner scanner,
        IMultiLevelCache cache,
        ICacheKeyGenerator keyGenerator,
        IDbExecutor dbExecutor,
        IOptions<CacheOptions> options,
        ILogger<CacheWarmer> logger)
    {
        _scanner = scanner;
        _cache = cache;
        _keyGenerator = keyGenerator;
        _dbExecutor = dbExecutor;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task WarmupAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || !_options.Warmup.EnableOnStartup)
        {
            _logger.LogInformation("Cache warmup is disabled");
            return;
        }

        var entities = _scanner.ScanEntities()
            .Where(e => e.Attribute.EnableWarmup)
            .ToList();

        if (entities.Count == 0)
        {
            _logger.LogInformation("No entities to warmup");
            return;
        }

        _logger.LogInformation("Starting cache warmup for {Count} entities", entities.Count);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.Warmup.TimeoutSeconds));

        var semaphore = new SemaphoreSlim(_options.Warmup.ParallelDegree);
        var tasks = new List<Task>();

        foreach (var metadata in entities)
        {
            var task = WarmupEntityInternalAsync(metadata, semaphore, cts.Token);
            tasks.Add(task);
        }

        try
        {
            await Task.WhenAll(tasks);
            _logger.LogInformation("Cache warmup completed");
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            _logger.LogWarning("Cache warmup timed out after {Timeout} seconds", _options.Warmup.TimeoutSeconds);

            if (_options.Warmup.FailOnError)
                throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cache warmup failed");

            if (_options.Warmup.FailOnError)
                throw;
        }
    }

    /// <inheritdoc />
    public Task WarmupEntityAsync<TEntity>(CancellationToken cancellationToken = default)
    {
        return WarmupEntityAsync(typeof(TEntity), cancellationToken);
    }

    /// <inheritdoc />
    public async Task WarmupEntityAsync(Type entityType, CancellationToken cancellationToken = default)
    {
        var metadata = _scanner.GetMetadata(entityType);

        if (metadata is null)
        {
            _logger.LogWarning("Entity {EntityType} is not cacheable", entityType.Name);
            return;
        }

        using var semaphore = new SemaphoreSlim(1);
        await WarmupEntityInternalAsync(metadata, semaphore, cancellationToken);
    }

    private async Task WarmupEntityInternalAsync(
        CacheableEntityMetadata metadata,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            _logger.LogDebug("Warming up entity: {EntityType}", metadata.EntityType.Name);

            var strategy = new CacheStrategyOptions
            {
                Levels = metadata.Attribute.Levels,
                ExpirationSeconds = metadata.Attribute.ExpirationSeconds > 0
                    ? metadata.Attribute.ExpirationSeconds
                    : _options.DefaultStrategy.ExpirationSeconds,
                CacheNullValue = false
            };

            if (metadata.Attribute.Granularity == CacheGranularity.TableLevel)
            {
                // 整表缓存
                await WarmupTableLevelAsync(metadata, strategy, cancellationToken);
            }
            else
            {
                // 按主键缓存
                await WarmupByPrimaryKeyAsync(metadata, strategy, cancellationToken);
            }

            _logger.LogInformation("Entity {EntityType} warmed up successfully", metadata.EntityType.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to warmup entity: {EntityType}", metadata.EntityType.Name);

            if (_options.Warmup.FailOnError)
                throw;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task WarmupTableLevelAsync(
        CacheableEntityMetadata metadata,
        CacheStrategyOptions strategy,
        CancellationToken cancellationToken)
    {
        // 使用动态 SQL 查询整表数据
        var sql = $"SELECT * FROM {metadata.TableName}";
        var data = await QueryDynamicAsync(metadata.EntityType, sql, cancellationToken);

        if (data.Count > 0)
        {
            var key = _keyGenerator.GenerateKey(metadata.EntityType);
            await _cache.SetAsync(key, data, strategy, cancellationToken);
            _logger.LogDebug("Cached {Count} rows for table {Table}", data.Count, metadata.TableName);
        }
    }

    private async Task WarmupByPrimaryKeyAsync(
        CacheableEntityMetadata metadata,
        CacheStrategyOptions strategy,
        CancellationToken cancellationToken)
    {
        if (metadata.KeyProperties.Count == 0)
        {
            _logger.LogWarning(
                "Entity {EntityType} has no key properties, skipping warmup",
                metadata.EntityType.Name);
            return;
        }

        // 查询需要预热的数据（按主键排序取前 N 条）
        var keyColumn = metadata.KeyProperties[0].Name;
        var limit = metadata.Attribute.MaxWarmupRows;
        var sql = $"SELECT * FROM {metadata.TableName} ORDER BY {keyColumn} LIMIT {limit}";

        var data = await QueryDynamicAsync(metadata.EntityType, sql, cancellationToken);

        foreach (var entity in data)
        {
            var primaryKeyValue = metadata.GetPrimaryKeyValue(entity);
            var key = _keyGenerator.GenerateKey(metadata.EntityType, primaryKeyValue);
            await _cache.SetAsync(key, entity, strategy, cancellationToken);
        }

        _logger.LogDebug("Cached {Count} entities for {EntityType}", data.Count, metadata.EntityType.Name);
    }

    private async Task<IReadOnlyList<object>> QueryDynamicAsync(
        Type entityType,
        string sql,
        CancellationToken cancellationToken)
    {
        // 使用反射调用泛型方法
        var method = typeof(IDbExecutor).GetMethod(nameof(IDbExecutor.QueryAsync))!;
        var genericMethod = method.MakeGenericMethod(entityType);

        var task = (Task)genericMethod.Invoke(_dbExecutor, [sql, null, cancellationToken])!;
        await task;

        var resultProperty = task.GetType().GetProperty("Result")!;
        var result = resultProperty.GetValue(task) as System.Collections.IEnumerable;

        return result?.Cast<object>().ToList() ?? [];
    }
}
