namespace JZVerse.MicroHuaxia.MessageQueue.Server.Management;

/// <summary>
/// Broker 管理查询接口
/// </summary>
public interface IBrokerManagement
{
    /// <summary>
    /// 获取 Broker 统计信息
    /// </summary>
    Task<BrokerStats> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有 Topic 列表
    /// </summary>
    Task<IReadOnlyList<TopicInfo>> GetTopicsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取 Topic 详情（含分区信息）
    /// </summary>
    Task<TopicDetail?> GetTopicDetailAsync(string topic, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取消费者组列表
    /// </summary>
    Task<IReadOnlyList<ConsumerGroupInfo>> GetConsumerGroupsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取客户端连接列表
    /// </summary>
    Task<IReadOnlyList<ClientConnectionInfo>> GetConnectionsAsync(CancellationToken cancellationToken = default);
}
