using System.Data;

namespace JZVerse.MicroHuaxia.DataAccess.Abstractions.Sharding;

/// <summary>
/// 数据库路由器接口 - 负责计算路由到哪个数据库节点和表
/// </summary>
public interface IDatabaseRouter
{
    /// <summary>
    /// 根据实体类型和分片键计算路由
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="shardingKey">分片键值</param>
    /// <param name="operationType">操作类型</param>
    /// <returns>路由结果</returns>
    RoutingResult Route<T>(object? shardingKey = null, DataOperationType operationType = DataOperationType.Query);

    /// <summary>
    /// 根据实体类型和分片键计算路由
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <param name="shardingKey">分片键值</param>
    /// <param name="operationType">操作类型</param>
    /// <returns>路由结果</returns>
    RoutingResult Route(Type entityType, object? shardingKey = null, DataOperationType operationType = DataOperationType.Query);

    /// <summary>
    /// 获取指定类型的所有分片表名（用于跨分片查询）
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <returns>所有分片表名列表</returns>
    List<string> GetAllShardTableNames<T>();

    /// <summary>
    /// 根据SQL语句解析路由信息
    /// </summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="parameters">参数</param>
    /// <returns>路由上下文</returns>
    RoutingContext? ParseRouteFromSql(string sql, object? parameters = null);
}

/// <summary>
/// 多数据库连接管理器接口 - 管理多个数据库集群和节点的连接
/// </summary>
public interface IMultiDbConnectionManager
{
    /// <summary>
    /// 获取数据库连接
    /// </summary>
    /// <param name="clusterName">集群名称</param>
    /// <param name="nodeIndex">节点索引</param>
    /// <returns>数据库连接</returns>
    IDbConnection GetConnection(string clusterName, int nodeIndex = 0);

    /// <summary>
    /// 异步获取并打开的数据库连接
    /// </summary>
    /// <param name="clusterName">集群名称</param>
    /// <param name="nodeIndex">节点索引</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已打开的数据库连接</returns>
    Task<IDbConnection> GetOpenConnectionAsync(string clusterName, int nodeIndex = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据路由结果获取连接
    /// </summary>
    /// <param name="routingResult">路由结果</param>
    /// <returns>数据库连接</returns>
    IDbConnection GetConnectionByRoute(RoutingResult routingResult);

    /// <summary>
    /// 根据路由结果异步获取并打开连接
    /// </summary>
    /// <param name="routingResult">路由结果</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已打开的数据库连接</returns>
    Task<IDbConnection> GetOpenConnectionByRouteAsync(RoutingResult routingResult, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取主节点连接（用于写操作）
    /// </summary>
    /// <param name="clusterName">集群名称</param>
    /// <returns>主节点连接</returns>
    IDbConnection GetMasterConnection(string clusterName);

    /// <summary>
    /// 获取从节点连接（用于读操作，自动负载均衡）
    /// </summary>
    /// <param name="clusterName">集群名称</param>
    /// <returns>从节点连接</returns>
    IDbConnection GetSlaveConnection(string clusterName);

    /// <summary>
    /// 获取集群配置
    /// </summary>
    /// <param name="clusterName">集群名称</param>
    /// <returns>集群配置</returns>
    DatabaseClusterConfig? GetClusterConfig(string clusterName);

    /// <summary>
    /// 测试指定节点的连接
    /// </summary>
    /// <param name="clusterName">集群名称</param>
    /// <param name="nodeIndex">节点索引</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>连接是否成功</returns>
    Task<bool> TestConnectionAsync(string clusterName, int nodeIndex = 0, CancellationToken cancellationToken = default);
}

/// <summary>
/// 实体元数据提供者接口 - 解析实体类的路由特性
/// </summary>
public interface IEntityMetadataProvider
{
    /// <summary>
    /// 获取实体类型的元数据
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>实体元数据</returns>
    EntityMetadata GetMetadata(Type entityType);

    /// <summary>
    /// 获取实体类型的元数据
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <returns>实体元数据</returns>
    EntityMetadata GetMetadata<T>();

    /// <summary>
    /// 获取实体的分片键值
    /// </summary>
    /// <param name="entity">实体对象</param>
    /// <returns>分片键值</returns>
    object? GetShardingKeyValue(object entity);

    /// <summary>
    /// 获取指定类型的分片属性信息
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>分片属性信息</returns>
    ShardingPropertyInfo? GetShardingPropertyInfo(Type entityType);
}

/// <summary>
/// 实体元数据
/// </summary>
public class EntityMetadata
{
    /// <summary>
    /// 实体类型
    /// </summary>
    public Type EntityType { get; set; } = null!;

    /// <summary>
    /// 逻辑表名
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// 实际表名（带占位符）
    /// </summary>
    public string? ActualTableNameFormat { get; set; }

    /// <summary>
    /// 数据库集群名称
    /// </summary>
    public string ClusterName { get; set; } = "default";

    /// <summary>
    /// 是否使用分片
    /// </summary>
    public bool UseSharding { get; set; }

    /// <summary>
    /// 分片策略类型
    /// </summary>
    public ShardingStrategyType ShardingStrategy { get; set; }

    /// <summary>
    /// 分片键字段名
    /// </summary>
    public string ShardingKey { get; set; } = "Id";

    /// <summary>
    /// 分片数
    /// </summary>
    public int ShardCount { get; set; } = 1;

    /// <summary>
    /// 列名映射（属性名 -> 列名）
    /// </summary>
    public Dictionary<string, string> ColumnMappings { get; set; } = new();

    /// <summary>
    /// 忽略的字段
    /// </summary>
    public List<string> IgnoredProperties { get; set; } = new();
}

/// <summary>
/// 分片属性信息
/// </summary>
public class ShardingPropertyInfo
{
    /// <summary>
    /// 属性名
    /// </summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>
    /// 属性类型
    /// </summary>
    public Type PropertyType { get; set; } = null!;

    /// <summary>
    /// 获取属性值的委托
    /// </summary>
    public Func<object, object?>? ValueGetter { get; set; }
}
