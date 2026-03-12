using System.Collections.Concurrent;
using System.Reflection;
using JZVerse.DataAccess.Abstractions.Caching;
using Microsoft.Extensions.Logging;

namespace JZVerse.DataAccess.Core.Caching;

/// <summary>
/// 可缓存实体扫描器实现
/// </summary>
public sealed class CacheableEntityScanner : ICacheableEntityScanner
{
    private readonly ILogger<CacheableEntityScanner> _logger;
    private readonly ConcurrentDictionary<Type, CacheableEntityMetadata?> _metadataCache = new();
    private IReadOnlyList<CacheableEntityMetadata>? _allEntities;
    private readonly object _scanLock = new();

    public CacheableEntityScanner(ILogger<CacheableEntityScanner> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyList<CacheableEntityMetadata> ScanEntities()
    {
        if (_allEntities is not null)
            return _allEntities;

        lock (_scanLock)
        {
            if (_allEntities is not null)
                return _allEntities;

            var entities = new List<CacheableEntityMetadata>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    // 跳过系统程序集
                    if (assembly.IsDynamic || assembly.FullName?.StartsWith("System") == true ||
                        assembly.FullName?.StartsWith("Microsoft") == true)
                        continue;

                    foreach (var type in assembly.GetTypes())
                    {
                        var metadata = GetMetadataInternal(type);
                        if (metadata is not null)
                        {
                            entities.Add(metadata);
                            _metadataCache[type] = metadata;
                        }
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    _logger.LogWarning(ex, "Failed to scan assembly {Assembly}", assembly.FullName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error scanning assembly {Assembly}", assembly.FullName);
                }
            }

            _allEntities = entities
                .OrderBy(e => e.Attribute.WarmupPriority)
                .ThenBy(e => e.EntityType.Name)
                .ToList();

            _logger.LogInformation("Scanned {Count} cacheable entities", _allEntities.Count);

            return _allEntities;
        }
    }

    /// <inheritdoc />
    public CacheableEntityMetadata? GetMetadata<TEntity>()
    {
        return GetMetadata(typeof(TEntity));
    }

    /// <inheritdoc />
    public CacheableEntityMetadata? GetMetadata(Type entityType)
    {
        return _metadataCache.GetOrAdd(entityType, GetMetadataInternal);
    }

    /// <inheritdoc />
    public bool IsCacheable<TEntity>()
    {
        return IsCacheable(typeof(TEntity));
    }

    /// <inheritdoc />
    public bool IsCacheable(Type entityType)
    {
        return GetMetadata(entityType) is not null;
    }

    private CacheableEntityMetadata? GetMetadataInternal(Type type)
    {
        var attribute = type.GetCustomAttribute<CacheableAttribute>();
        if (attribute is null)
            return null;

        // 获取所有标记了 CacheKey 的属性
        var keyProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => new { Property = p, Attribute = p.GetCustomAttribute<CacheKeyAttribute>() })
            .Where(x => x.Attribute is not null)
            .OrderBy(x => x.Attribute!.Order)
            .Select(x => new CacheKeyPropertyInfo
            {
                Name = x.Property.Name,
                PropertyType = x.Property.PropertyType,
                Order = x.Attribute!.Order,
                GetValue = CreatePropertyGetter(x.Property)
            })
            .ToList();

        // 如果没有标记 CacheKey，尝试查找名为 Id 或 {TypeName}Id 的属性
        if (keyProperties.Count == 0)
        {
            var idProperty = type.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)
                ?? type.GetProperty($"{type.Name}Id", BindingFlags.Public | BindingFlags.Instance);

            if (idProperty is not null)
            {
                keyProperties.Add(new CacheKeyPropertyInfo
                {
                    Name = idProperty.Name,
                    PropertyType = idProperty.PropertyType,
                    Order = 0,
                    GetValue = CreatePropertyGetter(idProperty)
                });
            }
        }

        if (keyProperties.Count == 0 && attribute.Granularity == CacheGranularity.ByPrimaryKey)
        {
            _logger.LogWarning(
                "Entity {EntityType} has [Cacheable] but no [CacheKey] property and no Id property found. " +
                "It will only support table-level caching.",
                type.Name);
        }

        var tableName = attribute.TableName ?? type.Name;

        return new CacheableEntityMetadata
        {
            EntityType = type,
            Attribute = attribute,
            KeyProperties = keyProperties,
            TableName = tableName
        };
    }

    private static Func<object, object?> CreatePropertyGetter(PropertyInfo property)
    {
        return entity => property.GetValue(entity);
    }
}
