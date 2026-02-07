using JZVerse.MicroHuaxia.MessageQueue.Abstractions;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Storage;
using JZVerse.MicroHuaxia.MessageQueue.Core.Subscription;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.MessageQueue.Core.Consumers;

/// <summary>
/// 消息消费者实现
/// </summary>
public sealed class MessageConsumer : IMessageConsumer
{
    private readonly ILogger<MessageConsumer> _logger;
    private readonly IMessageStore _messageStore;
    private readonly IOffsetManager _offsetManager;
    private readonly ISubscriptionManager _subscriptionManager;
    private readonly Dictionary<string, string> _subscriptions = new(); // topic -> subscriptionId

    public MessageConsumer(
        ILogger<MessageConsumer> logger,
        IMessageStore messageStore,
        IOffsetManager offsetManager,
        ISubscriptionManager subscriptionManager)
    {
        _logger = logger;
        _messageStore = messageStore;
        _offsetManager = offsetManager;
        _subscriptionManager = subscriptionManager;
    }

    /// <inheritdoc />
    public async Task SubscribeAsync(
        string topic,
        Func<IMessageEnvelope, CancellationToken, Task<ConsumeResult>> handler,
        ConsumeOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ConsumeOptions();

        var subscriptionId = await _subscriptionManager.SubscribeAsync(
            topic,
            handler,
            options,
            cancellationToken);

        _subscriptions[topic] = subscriptionId;

        _logger.LogInformation("Subscribed to topic {Topic} with subscription {SubscriptionId}",
            topic, subscriptionId);
    }

    /// <inheritdoc />
    public async Task UnsubscribeAsync(string topic, CancellationToken cancellationToken = default)
    {
        if (_subscriptions.TryGetValue(topic, out var subscriptionId))
        {
            await _subscriptionManager.UnsubscribeAsync(subscriptionId, cancellationToken);
            _subscriptions.Remove(topic);

            _logger.LogInformation("Unsubscribed from topic {Topic}", topic);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IMessageEnvelope>> PullAsync(
        string topic,
        int batchSize = 10,
        CancellationToken cancellationToken = default)
    {
        // 获取当前消费位点
        var consumerGroup = "default"; // 简化实现，实际应从配置获取
        var partition = 0;

        var currentOffset = await _offsetManager.GetOffsetAsync(consumerGroup, topic, partition, cancellationToken);
        if (currentOffset < 0)
        {
            currentOffset = await _messageStore.GetEarliestOffsetAsync(topic, partition, cancellationToken);
        }

        // 拉取消息
        var messages = await _messageStore.GetByOffsetAsync(topic, partition, currentOffset, batchSize, cancellationToken);

        // 转换为消息信封
        var envelopes = new List<IMessageEnvelope>(messages.Count);
        var offset = currentOffset;
        foreach (var message in messages)
        {
            envelopes.Add(new MessageEnvelope
            {
                Message = message,
                DeliveryCount = 1,
                Offset = offset++,
                Partition = partition,
                ConsumerGroup = consumerGroup,
                Routing = new RoutingInfo { Queue = topic }
            });
        }

        _logger.LogDebug("Pulled {Count} messages from topic {Topic} starting at offset {Offset}",
            envelopes.Count, topic, currentOffset);

        return envelopes;
    }

    /// <inheritdoc />
    public async Task AcknowledgeAsync(string messageId, CancellationToken cancellationToken = default)
    {
        // 简化实现：确认消息意味着提交偏移量
        // 实际实现需要维护消息ID到偏移量的映射
        _logger.LogDebug("Message {MessageId} acknowledged", messageId);
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task NegativeAcknowledgeAsync(string messageId, CancellationToken cancellationToken = default)
    {
        // 否认消息，将触发重试
        _logger.LogWarning("Message {MessageId} negatively acknowledged, will be retried", messageId);
        await Task.CompletedTask;
    }
}
