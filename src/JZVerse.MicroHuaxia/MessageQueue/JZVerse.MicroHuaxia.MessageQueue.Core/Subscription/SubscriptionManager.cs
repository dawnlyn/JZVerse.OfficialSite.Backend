using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.MessageQueue.Core.Subscription;

/// <summary>
/// 订阅信息
/// </summary>
public sealed record SubscriptionInfo
{
    /// <summary>
    /// 订阅ID
    /// </summary>
    public required string SubscriptionId { get; init; }

    /// <summary>
    /// 主题或队列名称
    /// </summary>
    public required string Target { get; init; }

    /// <summary>
    /// 消费者组
    /// </summary>
    public string? ConsumerGroup { get; init; }

    /// <summary>
    /// 消息处理器
    /// </summary>
    public required Func<IMessageEnvelope, CancellationToken, Task<ConsumeResult>> Handler { get; init; }

    /// <summary>
    /// 消费选项
    /// </summary>
    public ConsumeOptions Options { get; init; } = new();

    /// <summary>
    /// 订阅时间
    /// </summary>
    public DateTimeOffset SubscribedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 最后心跳时间
    /// </summary>
    public DateTimeOffset LastHeartbeat { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// 订阅管理器接口
/// </summary>
public interface ISubscriptionManager
{
    /// <summary>
    /// 注册订阅
    /// </summary>
    Task<string> SubscribeAsync(
        string target,
        Func<IMessageEnvelope, CancellationToken, Task<ConsumeResult>> handler,
        ConsumeOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取消订阅
    /// </summary>
    Task UnsubscribeAsync(string subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取主题的订阅者
    /// </summary>
    Task<IReadOnlyList<SubscriptionInfo>> GetSubscriptionsAsync(string target, CancellationToken cancellationToken = default);

    /// <summary>
    /// 分发消息到队列
    /// </summary>
    Task DispatchToQueueAsync(string queueName, IMessageEnvelope envelope, CancellationToken cancellationToken = default);

    /// <summary>
    /// 分发消息到主题
    /// </summary>
    Task DispatchToTopicAsync(string topic, IMessageEnvelope envelope, CancellationToken cancellationToken = default);
}

/// <summary>
/// 订阅管理器实现
/// </summary>
public sealed class SubscriptionManager : ISubscriptionManager
{
    private readonly ILogger<SubscriptionManager> _logger;

    // 按目标（主题/队列）存储订阅: target -> subscriptions
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, SubscriptionInfo>> _subscriptions = new();

    // 订阅ID索引: subscriptionId -> target
    private readonly ConcurrentDictionary<string, string> _subscriptionIndex = new();

    // 消费者组的轮询计数器
    private readonly ConcurrentDictionary<string, int> _roundRobinCounters = new();

    public SubscriptionManager(ILogger<SubscriptionManager> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<string> SubscribeAsync(
        string target,
        Func<IMessageEnvelope, CancellationToken, Task<ConsumeResult>> handler,
        ConsumeOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var subscriptionId = GenerateSubscriptionId();
        var subscription = new SubscriptionInfo
        {
            SubscriptionId = subscriptionId,
            Target = target,
            ConsumerGroup = options?.ConsumerGroup,
            Handler = handler,
            Options = options ?? new ConsumeOptions()
        };

        var targetSubscriptions = _subscriptions.GetOrAdd(target, _ => new ConcurrentDictionary<string, SubscriptionInfo>());
        targetSubscriptions[subscriptionId] = subscription;
        _subscriptionIndex[subscriptionId] = target;

        _logger.LogInformation("Subscription {SubscriptionId} created for {Target} (group: {ConsumerGroup})",
            subscriptionId, target, options?.ConsumerGroup ?? "none");

        return Task.FromResult(subscriptionId);
    }

    /// <inheritdoc />
    public Task UnsubscribeAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        if (!_subscriptionIndex.TryRemove(subscriptionId, out var target))
        {
            return Task.CompletedTask;
        }

        if (_subscriptions.TryGetValue(target, out var targetSubscriptions))
        {
            targetSubscriptions.TryRemove(subscriptionId, out _);
        }

        _logger.LogInformation("Subscription {SubscriptionId} removed from {Target}", subscriptionId, target);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SubscriptionInfo>> GetSubscriptionsAsync(string target, CancellationToken cancellationToken = default)
    {
        if (!_subscriptions.TryGetValue(target, out var targetSubscriptions))
        {
            return Task.FromResult<IReadOnlyList<SubscriptionInfo>>([]);
        }

        return Task.FromResult<IReadOnlyList<SubscriptionInfo>>(targetSubscriptions.Values.ToList());
    }

    /// <inheritdoc />
    public async Task DispatchToQueueAsync(string queueName, IMessageEnvelope envelope, CancellationToken cancellationToken = default)
    {
        if (!_subscriptions.TryGetValue(queueName, out var subscriptions) || subscriptions.IsEmpty)
        {
            _logger.LogDebug("No subscribers for queue {QueueName}", queueName);
            return;
        }

        // 队列模式：一条消息只被一个消费者处理（竞争消费）
        var subscription = SelectSubscriptionRoundRobin(queueName, subscriptions.Values.ToList());
        if (subscription != null)
        {
            await DispatchToSubscriptionAsync(subscription, envelope, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task DispatchToTopicAsync(string topic, IMessageEnvelope envelope, CancellationToken cancellationToken = default)
    {
        if (!_subscriptions.TryGetValue(topic, out var subscriptions) || subscriptions.IsEmpty)
        {
            _logger.LogDebug("No subscribers for topic {Topic}", topic);
            return;
        }

        // 主题模式：按消费者组分发（同组竞争，不同组广播）
        var groupedSubscriptions = subscriptions.Values
            .GroupBy(s => s.ConsumerGroup ?? Guid.NewGuid().ToString()) // 无组的独立消费
            .ToList();

        foreach (var group in groupedSubscriptions)
        {
            // 每组选择一个订阅者（轮询）
            var subscription = SelectSubscriptionRoundRobin(
                $"{topic}:{group.Key}",
                group.ToList());

            if (subscription != null)
            {
                await DispatchToSubscriptionAsync(subscription, envelope, cancellationToken);
            }
        }
    }

    private async Task DispatchToSubscriptionAsync(
        SubscriptionInfo subscription,
        IMessageEnvelope envelope,
        CancellationToken cancellationToken)
    {
        try
        {
            // 更新信封的消费者组信息
            var updatedEnvelope = new MessageEnvelope
            {
                Message = envelope.Message,
                DeliveryCount = envelope.DeliveryCount,
                LastDeliveryTime = DateTimeOffset.UtcNow,
                TraceContext = envelope.TraceContext,
                Routing = envelope.Routing,
                Offset = envelope.Offset,
                Partition = envelope.Partition,
                ConsumerGroup = subscription.ConsumerGroup
            };

            var result = await subscription.Handler(updatedEnvelope, cancellationToken);

            _logger.LogDebug("Message {MessageId} dispatched to subscription {SubscriptionId}, result: {Result}",
                envelope.Message.MessageId, subscription.SubscriptionId, result);

            // 处理结果
            switch (result)
            {
                case ConsumeResult.Success:
                    // 消费成功，无需额外处理
                    break;

                case ConsumeResult.RetryLater:
                    // 稍后重试（可以放入延迟队列）
                    _logger.LogWarning("Message {MessageId} will be retried later", envelope.Message.MessageId);
                    break;

                case ConsumeResult.Failure:
                    // 消费失败，进入死信队列
                    _logger.LogError("Message {MessageId} consumption failed, moving to DLQ", envelope.Message.MessageId);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dispatching message {MessageId} to subscription {SubscriptionId}",
                envelope.Message.MessageId, subscription.SubscriptionId);
        }
    }

    private SubscriptionInfo? SelectSubscriptionRoundRobin(string key, List<SubscriptionInfo> subscriptions)
    {
        if (subscriptions.Count == 0)
        {
            return null;
        }

        var counter = _roundRobinCounters.GetOrAdd(key, _ => 0);
        var index = Interlocked.Increment(ref counter) % subscriptions.Count;
        _roundRobinCounters[key] = counter;

        return subscriptions[index];
    }

    private static string GenerateSubscriptionId()
    {
        return $"sub-{Guid.NewGuid():N}";
    }
}
