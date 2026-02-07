using JZVerse.MicroHuaxia.MessageQueue.Abstractions;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;
using JZVerse.MicroHuaxia.MessageQueue.Core.Routing;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.MessageQueue.Protocol.InProc;

/// <summary>
/// 进程内消息生产者
/// </summary>
public sealed class InProcMessageProducer : IMessageProducer
{
    private readonly ILogger<InProcMessageProducer> _logger;
    private readonly IRoutingEngine _routingEngine;

    public InProcMessageProducer(
        ILogger<InProcMessageProducer> logger,
        IRoutingEngine routingEngine)
    {
        _logger = logger;
        _routingEngine = routingEngine;
    }

    /// <inheritdoc />
    public async Task<SendResult> SendAsync(
        IMessage message,
        SendOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 检查是否是延迟消息
            if (message.DelaySeconds.HasValue && message.DelaySeconds.Value > 0)
            {
                return await SendDelayedAsync(message, TimeSpan.FromSeconds(message.DelaySeconds.Value), cancellationToken);
            }

            // 直接路由到主题
            await _routingEngine.RouteToTopicAsync(message.Topic, message, cancellationToken);

            _logger.LogDebug("[InProc] Message {MessageId} sent to topic {Topic}",
                message.MessageId, message.Topic);

            return SendResult.Ok(message.MessageId, 0, 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[InProc] Failed to send message {MessageId}", message.MessageId);
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
    public Task<SendResult> SendDelayedAsync(
        IMessage message,
        TimeSpan delay,
        CancellationToken cancellationToken = default)
    {
        // 进程内延迟消息：使用 Task.Delay 实现简单延迟
        _ = Task.Run(async () =>
        {
            await Task.Delay(delay, cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
            {
                await _routingEngine.RouteToTopicAsync(message.Topic, message, CancellationToken.None);
                _logger.LogDebug("[InProc] Delayed message {MessageId} delivered after {Delay}",
                    message.MessageId, delay);
            }
        }, cancellationToken);

        return Task.FromResult(SendResult.Ok(message.MessageId, 0, 0));
    }
}
