namespace JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Clustering;

/// <summary>
/// 集群节点状态
/// </summary>
public enum NodeStatus
{
    /// <summary>
    /// 在线
    /// </summary>
    Online,

    /// <summary>
    /// 离线
    /// </summary>
    Offline,

    /// <summary>
    /// 同步中
    /// </summary>
    Syncing,
}

/// <summary>
/// 集群节点
/// </summary>
public sealed class ClusterNode
{
    /// <summary>
    /// 节点 ID
    /// </summary>
    public string NodeId { get; init; } = string.Empty;

    /// <summary>
    /// 节点主机
    /// </summary>
    public string Host { get; init; } = string.Empty;

    /// <summary>
    /// 节点端口
    /// </summary>
    public int Port { get; init; }

    /// <summary>
    /// 是否为领导者
    /// </summary>
    public bool IsLeader { get; set; }

    /// <summary>
    /// 节点状态
    /// </summary>
    public NodeStatus Status { get; set; }

    /// <summary>
    /// 最后心跳时间
    /// </summary>
    public DateTimeOffset LastHeartbeatAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// 集群管理器接口（预留）
/// </summary>
public interface IClusterManager
{
    /// <summary>
    /// 加入集群
    /// </summary>
    /// <param name="node">节点信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> JoinClusterAsync(ClusterNode node, CancellationToken cancellationToken = default);

    /// <summary>
    /// 离开集群
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> LeaveClusterAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取集群节点列表
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>节点列表</returns>
    Task<IReadOnlyList<ClusterNode>> GetNodesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 同步数据到其他节点
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task SyncDataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 选举领导者
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>领导者节点</returns>
    Task<ClusterNode> ElectLeaderAsync(CancellationToken cancellationToken = default);
}
