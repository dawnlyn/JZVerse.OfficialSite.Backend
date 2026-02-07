using JZVerse.MicroHuaxia.MessageQueue.Abstractions;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Storage;
using JZVerse.MicroHuaxia.MessageQueue.Core.Routing;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.MessageQueue.Core.Producers;

/// <summary>
/// 消息生产者实现
/// </summary>
public sealed class MessageProducer : IMessageProducer
{
    private readonly ILogger<MessageProducer> _logger;
    private readonly IMessageStore _messageStore;
    private readonly IRoutingEngine _routingEngine;

    public MessageProducer(
        ILogger<MessageProducer> logger,
        IMessageStore messageStore,
        IRoutingEngine routingEngine)
    {
        _logger = logger;
        _messageStore = messageStore;
        _routingEngine = routingEngine;
    }

    /// <inheritdoc />
    public async Task<SendResult> SendAsync(
        IMessage message,
        SendOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SendOptions();

        try
        {
            // 检查是否是延迟消息
            if (message.DelaySeconds.HasValue && message.DelaySeconds.Value > 0)
            {
                return await SendDelayedAsync(message, TimeSpan.FromSeconds(message.DelaySeconds.Value), cancellationToken);
            }

            // 路由消息到主题
            await _routingEngine.RouteToTopicAsync(message.Topic, message, cancellationToken);

            var offset = await _messageStore.GetLatestOffsetAsync(message.Topic, 0, cancellationToken);

            _logger.LogDebug("Message {MessageId} sent to topic {Topic}",
                message.MessageId, message.Topic);

            return SendResult.Ok(message.MessageId, offset, 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message {MessageId}", message.MessageId);
            return SendResult.Fail(ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SendResult>> SendBatchAsync(
        IEnumerable<IMessage> messages,
        SendOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<SendResult>();

        foreach (var message in messages)
        {
            var result = await SendAsync(message, options, cancellationToken);
            results.Add(result);
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<SendResult> SendDelayedAsync(
        IMessage message,
        TimeSpan delay,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 将消息发送到延迟队列（实际实现需要延迟调度器）
            // 这里简化处理，直接存储到特殊的延迟主题
            var delayedMessage = new Message
            {
                MessageId = message.MessageId,
                Topic = $"__delay_{(int)delay.TotalSeconds}s",
                Tag = message.Tag,
                Body = message.Body,
                Headers = new Dictionary<string, string>(message.Headers)
                {
                    ["__original_topic"] = message.Topic,
                    ["__delivery_time"] = DateTimeOffset.UtcNow.Add(delay).ToUnixTimeMilliseconds().ToString()
                },
                Timestamp = message.Timestamp,
                PartitionKey = message.PartitionKey,
                Priority = message.Priority
            };

            var offset = await _messageStore.AppendAsync(delayedMessage, 0, cancellationToken);

            _logger.LogDebug("Delayed message {MessageId} scheduled for delivery in {Delay}",
                message.MessageId, delay);

            return SendResult.Ok(message.MessageId, offset, 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send delayed message {MessageId}", message.MessageId);
            return SendResult.Fail(ex.Message);
        }
    }
}
