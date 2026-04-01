namespace JZVerse.MicroHuaxia.DataAccess.Abstractions.Sharding;

/// <summary>
/// 数据访问配置根选项
/// </summary>
public class DataAccessOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "DataAccess";

    /// <summary>
    /// 默认数据库连接名称
    /// </summary>
    public string DefaultConnectionName { get; set; } = "default";

    /// <summary>
    /// 数据库集群配置集合
    /// </summary>
    public Dictionary<string, DatabaseClusterConfig> Clusters { get; set; } = new();

    /// <summary>
    /// 分片规则配置集合
    /// </summary>
    public Dictionary<string, ShardingRuleConfig> ShardingRules { get; set; } = new();
}

/// <summary>
/// 数据库集群配置
/// </summary>
public class DatabaseClusterConfig
{
    /// <summary>
    /// 集群名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 数据库类型
    /// </summary>
    public string DatabaseType { get; set; } = "PostgreSql";

    /// <summary>
    /// 集群中的节点配置
    /// </summary>
    public List<DatabaseNodeConfig> Nodes { get; set; } = new();

    /// <summary>
    /// 默认分片数（用于哈希分片）
    /// </summary>
    public int DefaultShardCount { get; set; } = 1;

    /// <summary>
    /// 读写分离配置
    /// </summary>
    public ReadWriteSplittingConfig? ReadWriteSplitting { get; set; }
}

/// <summary>
/// 数据库节点配置
/// </summary>
public class DatabaseNodeConfig
{
    /// <summary>
    /// 节点名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 节点索引（用于分片路由）
    /// </summary>
    public int NodeIndex { get; set; }

    /// <summary>
    /// 连接字符串
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// 节点权重（用于负载均衡）
    /// </summary>
    public int Weight { get; set; } = 1;

    /// <summary>
    /// 是否为主节点
    /// </summary>
    public bool IsMaster { get; set; } = true;

    /// <summary>
    /// 分片范围配置（用于范围分片）
    /// </summary>
    public ShardRangeConfig? ShardRange { get; set; }
}

/// <summary>
/// 分片范围配置
/// </summary>
public class ShardRangeConfig
{
    /// <summary>
    /// 起始值（包含）
    /// </summary>
    public long StartValue { get; set; }

    /// <summary>
    /// 结束值（不包含）
    /// </summary>
    public long EndValue { get; set; }
}

/// <summary>
/// 读写分离配置
/// </summary>
public class ReadWriteSplittingConfig
{
    /// <summary>
    /// 是否启用读写分离
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// 从节点索引列表
    /// </summary>
    public List<int> SlaveNodeIndexes { get; set; } = new();

    /// <summary>
    /// 读取负载均衡策略（Random, RoundRobin, Weighted）
    /// </summary>
    public string LoadBalanceStrategy { get; set; } = "Random";
}

/// <summary>
/// 分片规则配置
/// </summary>
public class ShardingRuleConfig
{
    /// <summary>
    /// 规则名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 适用的实体类型全名（可选，如果指定了TablePattern则不需要）
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// 表名匹配模式（支持通配符 * 和 ?）
    /// </summary>
    public string? TablePattern { get; set; }

    /// <summary>
    /// 分片策略类型（Hash, Range, Time）
    /// </summary>
    public string Strategy { get; set; } = "Hash";

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
    public string TableNameFormat { get; set; } = "{0}";

    /// <summary>
    /// 目标数据库集群名称
    /// </summary>
    public string ClusterName { get; set; } = "default";

    /// <summary>
    /// 时间分片配置（仅当Strategy为Time时有效）
    /// </summary>
    public TimeShardingConfig? TimeSharding { get; set; }
}

/// <summary>
/// 时间分片配置
/// </summary>
public class TimeShardingConfig
{
    /// <summary>
    /// 时间分片粒度（Day, Week, Month, Year）
    /// </summary>
    public string Granularity { get; set; } = "Month";

    /// <summary>
    /// 表名日期格式（如：yyyyMM, yyyyMMdd）
    /// </summary>
    public string DateFormat { get; set; } = "yyyyMM";

    /// <summary>
    /// 历史表保留数量
    /// </summary>
    public int RetentionCount { get; set; } = 12;
}
