namespace JZVerse.DataAccess.Abstractions.Caching;

/// <summary>
/// 标记实体类为可缓存
/// </summary>
/// <remarks>
/// 配置优先级：特性配置 > 配置类 > 配置文件
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class CacheableAttribute : Attribute
{
    /// <summary>
    /// 缓存粒度
    /// </summary>
    /// <remarks>
    /// ByPrimaryKey: 按主键缓存单条记录，适合大表
    /// TableLevel: 整表缓存所有记录，适合小型配置表
    /// </remarks>
    public CacheGranularity Granularity { get; set; } = CacheGranularity.ByPrimaryKey;

    /// <summary>
    /// 启用的缓存级别
    /// </summary>
    /// <remarks>
    /// 可使用位运算组合多个级别：CacheLevel.Memory | CacheLevel.Redis
    /// </remarks>
    public CacheLevel Levels { get; set; } = CacheLevel.Memory | CacheLevel.Redis;

    /// <summary>
    /// 过期时间（秒）
    /// </summary>
    /// <remarks>
    /// -1 表示永不过期，0 表示使用配置文件中的默认值
    /// </remarks>
    public int ExpirationSeconds { get; set; } = 0;

    /// <summary>
    /// 是否在启动时预热缓存
    /// </summary>
    public bool EnableWarmup { get; set; } = true;

    /// <summary>
    /// 预热时最多加载的行数
    /// </summary>
    /// <remarks>
    /// 仅对 ByPrimaryKey 粒度有效，TableLevel 会加载全部数据
    /// </remarks>
    public int MaxWarmupRows { get; set; } = 10000;

    /// <summary>
    /// 预热优先级（数字越小越先执行）
    /// </summary>
    public int WarmupPriority { get; set; } = 100;

    /// <summary>
    /// 数据库表名（可选，默认使用实体类名）
    /// </summary>
    public string? TableName { get; set; }

    /// <summary>
    /// 是否缓存空值（防止缓存穿透）
    /// </summary>
    public bool CacheNullValue { get; set; } = true;
}
