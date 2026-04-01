namespace JZVerse.MicroHuaxia.DataAccess.Abstractions.Sharding;

/// <summary>
/// 路由上下文 - 包含分片路由所需的信息
/// </summary>
public class RoutingContext
{
    /// <summary>
    /// 实体类型
    /// </summary>
    public Type EntityType { get; set; } = null!;

    /// <summary>
    /// 逻辑表名
    /// </summary>
    public string LogicalTableName { get; set; } = string.Empty;

    /// <summary>
    /// 实际表名（分片后）
    /// </summary>
    public string? ActualTableName { get; set; }

    /// <summary>
    /// 分片键值
    /// </summary>
    public object? ShardingKey { get; set; }

    /// <summary>
    /// 分片索引
    /// </summary>
    public int? ShardIndex { get; set; }

    /// <summary>
    /// 数据库集群名称
    /// </summary>
    public string ClusterName { get; set; } = string.Empty;

    /// <summary>
    /// 节点索引
    /// </summary>
    public int? NodeIndex { get; set; }

    /// <summary>
    /// 是否使用主节点（写操作或强制主节点读）
    /// </summary>
    public bool UseMaster { get; set; } = true;

    /// <summary>
    /// 操作类型
    /// </summary>
    public DataOperationType OperationType { get; set; } = DataOperationType.Query;

    /// <summary>
    /// 附加数据
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// 数据操作类型
/// </summary>
public enum DataOperationType
{
    /// <summary>
    /// 查询
    /// </summary>
    Query,

    /// <summary>
    /// 插入
    /// </summary>
    Insert,

    /// <summary>
    /// 更新
    /// </summary>
    Update,

    /// <summary>
    /// 删除
    /// </summary>
    Delete,

    /// <summary>
    /// 批量操作
    /// </summary>
    Batch
}

/// <summary>
/// 路由结果
/// </summary>
public class RoutingResult
{
    /// <summary>
    /// 数据库集群名称
    /// </summary>
    public string ClusterName { get; set; } = string.Empty;

    /// <summary>
    /// 节点名称
    /// </summary>
    public string NodeName { get; set; } = string.Empty;

    /// <summary>
    /// 节点索引
    /// </summary>
    public int NodeIndex { get; set; }

    /// <summary>
    /// 逻辑表名
    /// </summary>
    public string LogicalTableName { get; set; } = string.Empty;

    /// <summary>
    /// 实际表名（已替换分片后缀）
    /// </summary>
    public string ActualTableName { get; set; } = string.Empty;

    /// <summary>
    /// 分片索引
    /// </summary>
    public int? ShardIndex { get; set; }

    /// <summary>
    /// 连接字符串
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 创建成功的路由结果
    /// </summary>
    public static RoutingResult Success(
        string clusterName,
        string nodeName,
        int nodeIndex,
        string logicalTableName,
        string actualTableName,
        string connectionString,
        int? shardIndex = null)
    {
        return new RoutingResult
        {
            IsSuccess = true,
            ClusterName = clusterName,
            NodeName = nodeName,
            NodeIndex = nodeIndex,
            LogicalTableName = logicalTableName,
            ActualTableName = actualTableName,
            ConnectionString = connectionString,
            ShardIndex = shardIndex
        };
    }

    /// <summary>
    /// 创建失败的路由结果
    /// </summary>
    public static RoutingResult Failure(string errorMessage)
    {
        return new RoutingResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}
