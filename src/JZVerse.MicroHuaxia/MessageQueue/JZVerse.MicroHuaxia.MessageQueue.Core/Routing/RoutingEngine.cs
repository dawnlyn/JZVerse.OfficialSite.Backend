using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Storage;
using JZVerse.MicroHuaxia.MessageQueue.Core.Subscription;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.MessageQueue.Core.Routing;

/// <summary>
/// 路由引擎实现
/// </summary>
public sealed class RoutingEngine : IRoutingEngine
{
    private readonly ILogger<RoutingEngine> _logger;
    private readonly IMessageStore _messageStore;
    private readonly ISubscriptionManager _subscriptionManager;

    public RoutingEngine(
        ILogger<RoutingEngine> logger,
        IMessageStore messageStore,
        ISubscriptionManager subscriptionManager)
    {
        _logger = logger;
        _messageStore = messageStore;
        _subscriptionManager = subscriptionManager;
    }

    /// <inheritdoc />
    public async Task RouteToQueueAsync(string queueName, IMessage message, CancellationToken cancellationToken = default)
    {
        // 存储消息
        var offset = await _messageStore.AppendAsync(message, partition: 0, cancellationToken);

        // 创建消息信封
        var envelope = new MessageEnvelope
        {
            Message = message,
            DeliveryCount = 1,
            Offset = offset,
            Partition = 0,
            Routing = new RoutingInfo
            {
                Queue = queueName
            }
        };

        // 分发给队列的订阅者
        await _subscriptionManager.DispatchToQueueAsync(queueName, envelope, cancellationToken);

        _logger.LogDebug("Message {MessageId} routed to queue {QueueName} at offset {Offset}",
            message.MessageId, queueName, offset);
    }

    /// <inheritdoc />
    public async Task RouteToTopicAsync(string topic, IMessage message, CancellationToken cancellationToken = default)
    {
        // 计算分区（基于 PartitionKey 或轮询）
        var partition = CalculatePartition(message);

        // 存储消息
        var offset = await _messageStore.AppendAsync(message, partition, cancellationToken);

        // 创建消息信封
        var envelope = new MessageEnvelope
        {
            Message = message,
            DeliveryCount = 1,
            Offset = offset,
            Partition = partition,
            Routing = new RoutingInfo
            {
                Queue = topic
            }
        };

        // 分发给主题的订阅者
        await _subscriptionManager.DispatchToTopicAsync(topic, envelope, cancellationToken);

        _logger.LogDebug("Message {MessageId} routed to topic {Topic}:{Partition} at offset {Offset}",
            message.MessageId, topic, partition, offset);
    }

    private int CalculatePartition(IMessage message)
    {
        // 如果有 PartitionKey，使用一致性哈希
        if (!string.IsNullOrEmpty(message.PartitionKey))
        {
            return Math.Abs(message.PartitionKey.GetHashCode()) % 16; // 默认 16 分区
        }

        // 否则使用轮询（简化实现，实际应该按主题维护计数器）
        return Random.Shared.Next(16);
    }
}
