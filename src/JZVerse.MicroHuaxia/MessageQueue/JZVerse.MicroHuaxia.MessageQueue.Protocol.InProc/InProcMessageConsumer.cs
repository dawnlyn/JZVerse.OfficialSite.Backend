using JZVerse.MicroHuaxia.MessageQueue.Abstractions;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Storage;
using JZVerse.MicroHuaxia.MessageQueue.Core.Subscription;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.MessageQueue.Protocol.InProc;

/// <summary>
/// 进程内消息消费者
/// </summary>
public sealed class InProcMessageConsumer : IMessageConsumer
{
    private readonly ILogger<InProcMessageConsumer> _logger;
    private readonly IMessageStore _messageStore;
    private readonly IOffsetManager _offsetManager;
    private readonly ISubscriptionManager _subscriptionManager;
    private readonly Dictionary<string, string> _subscriptions = new();
    private readonly string _consumerGroup;

    public InProcMessageConsumer(
        ILogger<InProcMessageConsumer> logger,
        IMessageStore messageStore,
        IOffsetManager offsetManager,
        ISubscriptionManager subscriptionManager,
        string? consumerGroup = null)
    {
        _logger = logger;
        _messageStore = messageStore;
        _offsetManager = offsetManager;
        _subscriptionManager = subscriptionManager;
        _consumerGroup = consumerGroup ?? $"inproc-{Guid.NewGuid():N}";
    }

    /// <inheritdoc />
    public async Task SubscribeAsync(
        string topic,
        Func<IMessageEnvelope, CancellationToken, Task<ConsumeResult>> handler,
        ConsumeOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ConsumeOptions { ConsumerGroup = _consumerGroup };

        if (string.IsNullOrEmpty(options.ConsumerGroup))
        {
            options = options with { ConsumerGroup = _consumerGroup };
        }

        var subscriptionId = await _subscriptionManager.SubscribeAsync(
            topic,
            handler,
            options,
            cancellationToken);

        _subscriptions[topic] = subscriptionId;

        _logger.LogInformation("[InProc] Subscribed to topic {Topic} with subscription {SubscriptionId}",
            topic, subscriptionId);
    }

    /// <inheritdoc />
    public async Task UnsubscribeAsync(string topic, CancellationToken cancellationToken = default)
    {
        if (_subscriptions.TryGetValue(topic, out var subscriptionId))
        {
            await _subscriptionManager.UnsubscribeAsync(subscriptionId, cancellationToken);
            _subscriptions.Remove(topic);

            _logger.LogInformation("[InProc] Unsubscribed from topic {Topic}", topic);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IMessageEnvelope>> PullAsync(
        string topic,
        int batchSize = 10,
        CancellationToken cancellationToken = default)
    {
        var partition = 0;

        var currentOffset = await _offsetManager.GetOffsetAsync(_consumerGroup, topic, partition, cancellationToken);
        if (currentOffset < 0)
        {
            currentOffset = await _messageStore.GetEarliestOffsetAsync(topic, partition, cancellationToken);
        }
        else
        {
            currentOffset++; // 从下一条消息开始
        }

        var messages = await _messageStore.GetByOffsetAsync(topic, partition, currentOffset, batchSize, cancellationToken);

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
                ConsumerGroup = _consumerGroup,
                Routing = new RoutingInfo { Queue = topic }
            });
        }

        _logger.LogDebug("[InProc] Pulled {Count} messages from topic {Topic} starting at offset {Offset}",
            envelopes.Count, topic, currentOffset);

        return envelopes;
    }

    /// <inheritdoc />
    public async Task AcknowledgeAsync(string messageId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[InProc] Message {MessageId} acknowledged", messageId);
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task NegativeAcknowledgeAsync(string messageId, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("[InProc] Message {MessageId} negatively acknowledged", messageId);
        await Task.CompletedTask;
    }
}

/// <summary>
/// 进程内消费者工厂
/// </summary>
public sealed class InProcMessageConsumerFactory
{
    private readonly ILogger<InProcMessageConsumer> _logger;
    private readonly IMessageStore _messageStore;
    private readonly IOffsetManager _offsetManager;
    private readonly ISubscriptionManager _subscriptionManager;

    public InProcMessageConsumerFactory(
        ILogger<InProcMessageConsumer> logger,
        IMessageStore messageStore,
        IOffsetManager offsetManager,
        ISubscriptionManager subscriptionManager)
    {
        _logger = logger;
        _messageStore = messageStore;
        _offsetManager = offsetManager;
        _subscriptionManager = subscriptionManager;
    }

    /// <summary>
    /// 创建消费者
    /// </summary>
    public IMessageConsumer Create(string? consumerGroup = null)
    {
        return new InProcMessageConsumer(
            _logger,
            _messageStore,
            _offsetManager,
            _subscriptionManager,
            consumerGroup);
    }
}
