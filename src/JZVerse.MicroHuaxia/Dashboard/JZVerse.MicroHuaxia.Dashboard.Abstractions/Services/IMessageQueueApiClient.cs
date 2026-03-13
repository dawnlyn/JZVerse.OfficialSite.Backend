using JZVerse.MicroHuaxia.Dashboard.Abstractions.Models;

namespace JZVerse.MicroHuaxia.Dashboard.Abstractions.Services;

/// <summary>
/// 消息队列 API 客户端接口
/// </summary>
public interface IMessageQueueApiClient
{
    /// <summary>获取 MQ 统计信息</summary>
    Task<MessageQueueStats> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>获取 Topic 列表</summary>
    Task<List<MqTopicInfo>> GetTopicsAsync(CancellationToken cancellationToken = default);

    /// <summary>获取 Topic 详情</summary>
    Task<MqTopicDetail?> GetTopicDetailAsync(string topicName, CancellationToken cancellationToken = default);

    /// <summary>获取消费者组列表</summary>
    Task<List<MqConsumerGroupInfo>> GetConsumerGroupsAsync(CancellationToken cancellationToken = default);

    /// <summary>获取客户端连接列表</summary>
    Task<List<MqClientConnectionInfo>> GetConnectionsAsync(CancellationToken cancellationToken = default);
}
