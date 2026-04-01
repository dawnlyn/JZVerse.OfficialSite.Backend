namespace JZVerse.MicroHuaxia.DataAccess.Abstractions.Sharding;

/// <summary>
/// 数据库路由特性 - 用于标记实体类对应的数据库集群
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class DatabaseRouteAttribute : Attribute
{
    /// <summary>
    /// 数据库集群名称
    /// </summary>
    public string ClusterName { get; }

    /// <summary>
    /// 是否使用分片
    /// </summary>
    public bool UseSharding { get; set; } = true;

    /// <summary>
    /// 创建数据库路由特性
    /// </summary>
    /// <param name="clusterName">数据库集群名称</param>
    public DatabaseRouteAttribute(string clusterName)
    {
        ClusterName = clusterName ?? throw new ArgumentNullException(nameof(clusterName));
    }
}

/// <summary>
/// 表路由特性 - 用于标记实体类对应的表名和分片策略
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class TableRouteAttribute : Attribute
{
    /// <summary>
    /// 逻辑表名
    /// </summary>
    public string TableName { get; }

    /// <summary>
    /// 分片策略类型
    /// </summary>
    public ShardingStrategyType Strategy { get; set; } = ShardingStrategyType.Hash;

    /// <summary>
    /// 分片键字段名
    /// </summary>
    public string ShardingKey { get; set; } = "Id";

    /// <summary>
    /// 分片数（用于哈希分片）
    /// </summary>
    public int ShardCount { get; set; } = 1;

    /// <summary>
    /// 表名格式模板（如：orders_{0:000}）
    /// </summary>
    public string? TableNameFormat { get; set; }

    /// <summary>
    /// 创建表路由特性
    /// </summary>
    /// <param name="tableName">逻辑表名</param>
    public TableRouteAttribute(string tableName)
    {
        TableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
    }
}

/// <summary>
/// 分片策略类型
/// </summary>
public enum ShardingStrategyType
{
    /// <summary>
    /// 不分片
    /// </summary>
    None,

    /// <summary>
    /// 哈希分片
    /// </summary>
    Hash,

    /// <summary>
    /// 范围分片
    /// </summary>
    Range,

    /// <summary>
    /// 时间分片
    /// </summary>
    Time
}

/// <summary>
/// 分片键特性 - 用于标记实体类的分片键字段
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public class ShardingKeyAttribute : Attribute
{
    /// <summary>
    /// 分片键顺序（用于复合分片键）
    /// </summary>
    public int Order { get; set; } = 0;
}

/// <summary>
/// 数据库忽略特性 - 用于标记不需要持久化的属性
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public class DbIgnoreAttribute : Attribute
{
}

/// <summary>
/// 列名特性 - 用于指定属性对应的数据库列名
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public class ColumnNameAttribute : Attribute
{
    /// <summary>
    /// 列名
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 创建列名特性
    /// </summary>
    /// <param name="name">列名</param>
    public ColumnNameAttribute(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }
}
