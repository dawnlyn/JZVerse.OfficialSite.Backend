using JZVerse.DataAccess.Abstractions.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.DataAccess.Caching.EFCore;

/// <summary>
/// EF Core 缓存拦截器
/// </summary>
/// <remarks>
/// 拦截 SaveChanges 操作，在数据变更后自动失效相关缓存
/// </remarks>
public sealed class EFCoreCacheInterceptor : SaveChangesInterceptor
{
    private readonly IMultiLevelCache _cache;
    private readonly ICacheKeyGenerator _keyGenerator;
    private readonly ICacheableEntityScanner _scanner;
    private readonly CacheOptions _options;
    private readonly ILogger<EFCoreCacheInterceptor> _logger;

    public EFCoreCacheInterceptor(
        IMultiLevelCache cache,
        ICacheKeyGenerator keyGenerator,
        ICacheableEntityScanner scanner,
        IOptions<CacheOptions> options,
        ILogger<EFCoreCacheInterceptor> logger)
    {
        _cache = cache;
        _keyGenerator = keyGenerator;
        _scanner = scanner;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || eventData.Context is null)
        {
            return await base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        await InvalidateChangedEntitiesAsync(eventData.Context, cancellationToken);

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    /// <inheritdoc />
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (!_options.Enabled || eventData.Context is null)
        {
            return base.SavedChanges(eventData, result);
        }

        InvalidateChangedEntitiesAsync(eventData.Context, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        return base.SavedChanges(eventData, result);
    }

    private async Task InvalidateChangedEntitiesAsync(DbContext context, CancellationToken cancellationToken)
    {
        try
        {
            var changedEntities = context.ChangeTracker.Entries()
                .Where(e => e.State is EntityState.Modified or EntityState.Deleted or EntityState.Added)
                .Select(e => new
                {
                    EntityType = e.Entity.GetType(),
                    Entity = e.Entity,
                    State = e.State
                })
                .ToList();

            var invalidatedTypes = new HashSet<Type>();

            foreach (var entry in changedEntities)
            {
                var metadata = _scanner.GetMetadata(entry.EntityType);

                if (metadata is null)
                    continue;

                if (metadata.Attribute.Granularity == CacheGranularity.ByPrimaryKey &&
                    metadata.KeyProperties.Count > 0)
                {
                    // 按主键失效
                    var primaryKey = metadata.GetPrimaryKeyValue(entry.Entity);
                    var key = _keyGenerator.GenerateKey(entry.EntityType, primaryKey);
                    await _cache.InvalidateAsync(key, cancellationToken);

                    _logger.LogDebug(
                        "Cache invalidated for entity {EntityType} with key {Key}",
                        entry.EntityType.Name,
                        primaryKey);
                }
                else if (!invalidatedTypes.Contains(entry.EntityType))
                {
                    // 整表失效（避免重复失效）
                    var prefix = _keyGenerator.GenerateEntityPrefix(entry.EntityType);
                    await _cache.InvalidateAsync(prefix, cancellationToken);
                    invalidatedTypes.Add(entry.EntityType);

                    _logger.LogDebug(
                        "Cache invalidated for entity type {EntityType}",
                        entry.EntityType.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate cache after SaveChanges");
        }
    }
}
