namespace JZVerse.MicroHuaxia.DataAccess.Abstractions.Caching;

/// <summary>
/// 可缓存实体扫描器接口
/// </summary>
public interface ICacheableEntityScanner
{
    /// <summary>
    /// 扫描所有标记了 [Cacheable] 特性的实体类型
    /// </summary>
    /// <returns>可缓存实体元数据列表</returns>
    IReadOnlyList<CacheableEntityMetadata> ScanEntities();

    /// <summary>
    /// 获取指定类型的缓存元数据
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <returns>缓存元数据，未标记返回 null</returns>
    CacheableEntityMetadata? GetMetadata<TEntity>();

    /// <summary>
    /// 获取指定类型的缓存元数据
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>缓存元数据，未标记返回 null</returns>
    CacheableEntityMetadata? GetMetadata(Type entityType);

    /// <summary>
    /// 检查类型是否可缓存
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <returns>是否可缓存</returns>
    bool IsCacheable<TEntity>();

    /// <summary>
    /// 检查类型是否可缓存
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>是否可缓存</returns>
    bool IsCacheable(Type entityType);
}

/// <summary>
/// 可缓存实体元数据
/// </summary>
public sealed class CacheableEntityMetadata
{
    /// <summary>
    /// 实体类型
    /// </summary>
    public required Type EntityType { get; init; }

    /// <summary>
    /// 缓存特性配置
    /// </summary>
    public required CacheableAttribute Attribute { get; init; }

    /// <summary>
    /// 主键属性列表（按 Order 排序）
    /// </summary>
    public required IReadOnlyList<CacheKeyPropertyInfo> KeyProperties { get; init; }

    /// <summary>
    /// 表名
    /// </summary>
    public required string TableName { get; init; }

    /// <summary>
    /// 获取实体实例的主键值
    /// </summary>
    /// <param name="entity">实体实例</param>
    /// <returns>主键值（复合主键用下划线连接）</returns>
    public string GetPrimaryKeyValue(object entity)
    {
        if (KeyProperties.Count == 0)
            throw new InvalidOperationException($"Entity type {EntityType.Name} has no cache key properties");

        if (KeyProperties.Count == 1)
            return KeyProperties[0].GetValue(entity)?.ToString() ?? string.Empty;

        return string.Join("_", KeyProperties.Select(p => p.GetValue(entity)?.ToString() ?? string.Empty));
    }
}

/// <summary>
/// 缓存键属性信息
/// </summary>
public sealed class CacheKeyPropertyInfo
{
    /// <summary>
    /// 属性名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 属性类型
    /// </summary>
    public required Type PropertyType { get; init; }

    /// <summary>
    /// 排序顺序
    /// </summary>
    public required int Order { get; init; }

    /// <summary>
    /// 获取属性值的委托
    /// </summary>
    public required Func<object, object?> GetValue { get; init; }
}
