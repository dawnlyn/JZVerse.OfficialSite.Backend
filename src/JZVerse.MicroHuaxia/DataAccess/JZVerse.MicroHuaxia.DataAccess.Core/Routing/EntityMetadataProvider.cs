using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using JZVerse.MicroHuaxia.DataAccess.Abstractions.Sharding;

namespace JZVerse.MicroHuaxia.DataAccess.Core.Routing;

/// <summary>
/// 实体元数据提供者实现
/// </summary>
public class EntityMetadataProvider : IEntityMetadataProvider
{
    private readonly ConcurrentDictionary<Type, EntityMetadata> _metadataCache = new();
    private readonly ConcurrentDictionary<Type, ShardingPropertyInfo?> _shardingPropertyCache = new();

    /// <inheritdoc />
    public EntityMetadata GetMetadata<T>()
    {
        return GetMetadata(typeof(T));
    }

    /// <inheritdoc />
    public EntityMetadata GetMetadata(Type entityType)
    {
        return _metadataCache.GetOrAdd(entityType, type =>
        {
            var metadata = new EntityMetadata
            {
                EntityType = type,
                TableName = type.Name
            };

            // 解析 DatabaseRouteAttribute
            var dbRouteAttr = type.GetCustomAttribute<DatabaseRouteAttribute>();
            if (dbRouteAttr != null)
            {
                metadata.ClusterName = dbRouteAttr.ClusterName;
                metadata.UseSharding = dbRouteAttr.UseSharding;
            }

            // 解析 TableRouteAttribute
            var tableRouteAttr = type.GetCustomAttribute<TableRouteAttribute>();
            if (tableRouteAttr != null)
            {
                metadata.TableName = tableRouteAttr.TableName;
                metadata.ShardingStrategy = tableRouteAttr.Strategy;
                metadata.ShardingKey = tableRouteAttr.ShardingKey;
                metadata.ShardCount = tableRouteAttr.ShardCount;
                metadata.ActualTableNameFormat = tableRouteAttr.TableNameFormat;
                metadata.UseSharding = tableRouteAttr.Strategy != ShardingStrategyType.None;
            }
            else
            {
                // 默认使用类名作为表名，不开启分片
                metadata.ShardingStrategy = ShardingStrategyType.None;
                metadata.UseSharding = false;
            }

            // 解析属性特性
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                // DbIgnoreAttribute
                if (property.GetCustomAttribute<DbIgnoreAttribute>() != null)
                {
                    metadata.IgnoredProperties.Add(property.Name);
                    continue;
                }

                // ColumnNameAttribute
                var columnAttr = property.GetCustomAttribute<ColumnNameAttribute>();
                if (columnAttr != null)
                {
                    metadata.ColumnMappings[property.Name] = columnAttr.Name;
                }
                else
                {
                    metadata.ColumnMappings[property.Name] = property.Name;
                }
            }

            return metadata;
        });
    }

    /// <inheritdoc />
    public object? GetShardingKeyValue(object entity)
    {
        var entityType = entity.GetType();
        var shardingInfo = GetShardingPropertyInfo(entityType);

        if (shardingInfo?.ValueGetter == null)
        {
            return null;
        }

        return shardingInfo.ValueGetter(entity);
    }

    /// <inheritdoc />
    public ShardingPropertyInfo? GetShardingPropertyInfo(Type entityType)
    {
        return _shardingPropertyCache.GetOrAdd(entityType, type =>
        {
            var metadata = GetMetadata(type);

            if (!metadata.UseSharding)
            {
                return null;
            }

            // 首先查找标记了 ShardingKeyAttribute 的属性
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            var shardingProp = properties
                .FirstOrDefault(p => p.GetCustomAttribute<ShardingKeyAttribute>() != null);

            if (shardingProp == null)
            {
                // 如果没有标记特性，按名称查找
                shardingProp = properties.FirstOrDefault(p =>
                    p.Name.Equals(metadata.ShardingKey, StringComparison.OrdinalIgnoreCase));
            }

            if (shardingProp == null)
            {
                return null;
            }

            // 创建属性值获取委托
            var getter = CreatePropertyGetter(type, shardingProp);

            return new ShardingPropertyInfo
            {
                PropertyName = shardingProp.Name,
                PropertyType = shardingProp.PropertyType,
                ValueGetter = getter
            };
        });
    }

    private static Func<object, object?>? CreatePropertyGetter(Type type, PropertyInfo property)
    {
        try
        {
            // 创建委托: (object instance) => (object?)property.GetValue(instance)
            var instanceParam = Expression.Parameter(typeof(object), "instance");
            var castInstance = Expression.Convert(instanceParam, type);
            var propertyAccess = Expression.Property(castInstance, property);
            var castResult = Expression.Convert(propertyAccess, typeof(object));
            var lambda = Expression.Lambda<Func<object, object?>>(castResult, instanceParam);
            return lambda.Compile();
        }
        catch
        {
            // 如果反射编译失败，使用传统反射
            return instance => property.GetValue(instance);
        }
    }
}
