using System.Diagnostics;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Tracing;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.MessageQueue.Core.Tracing;

/// <summary>
/// 追踪器配置
/// </summary>
public sealed class MessageTracerOptions
{
    /// <summary>
    /// 是否启用追踪
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// 节点标识
    /// </summary>
    public string NodeId { get; set; } = Environment.MachineName;
    
    /// <summary>
    /// 采样率（0-1）
    /// </summary>
    public double SamplingRate { get; set; } = 1.0;
    
    /// <summary>
    /// 是否记录消息体
    /// </summary>
    public bool IncludeMessageBody { get; set; } = false;
}

/// <summary>
/// 消息追踪服务实现
/// </summary>
public sealed class MessageTracer : IMessageTracer
{
    private readonly ILogger<MessageTracer> _logger;
    private readonly IMessageTraceStore _traceStore;
    private readonly MessageTracerOptions _options;

    public MessageTracer(
        ILogger<MessageTracer> logger,
        IMessageTraceStore traceStore,
        MessageTracerOptions? options = null)
    {
        _logger = logger;
        _traceStore = traceStore;
        _options = options ?? new MessageTracerOptions();
    }

    /// <inheritdoc />
    public async Task RecordAsync(MessageTracePoint tracePoint, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return;
        }
        
        // 采样判断
        if (_options.SamplingRate < 1.0 && Random.Shared.NextDouble() > _options.SamplingRate)
        {
            return;
        }
        
        await _traceStore.StoreAsync(tracePoint, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MessageTrace?> GetTraceAsync(string messageId, CancellationToken cancellationToken = default)
    {
        var tracePoints = await _traceStore.GetByMessageIdAsync(messageId, cancellationToken);
        
        if (tracePoints.Count == 0)
        {
            return null;
        }
        
        return new MessageTrace
        {
            MessageId = messageId,
            TracePoints = tracePoints
        };
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<MessageTracePoint>> QueryByTopicAsync(
        string topic,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default)
    {
        return _traceStore.QueryAsync(topic, null, startTime, endTime, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<MessageTracePoint>> QueryByConsumerGroupAsync(
        string consumerGroup,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default)
    {
        return _traceStore.QueryAsync(null, consumerGroup, startTime, endTime, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 创建追踪点（便捷方法）
    /// </summary>
    public MessageTracePoint CreateTracePoint(
        IMessage message,
        TracePointType type,
        string? consumerGroup = null,
        int? partition = null,
        long? offset = null,
        long? durationMs = null,
        bool success = true,
        string? error = null)
    {
        return new MessageTracePoint
        {
            TracePointId = GenerateTracePointId(),
            MessageId = message.MessageId,
            Type = type,
            Timestamp = DateTimeOffset.UtcNow,
            NodeId = _options.NodeId,
            Topic = message.Topic,
            ConsumerGroup = consumerGroup,
            Partition = partition,
            Offset = offset,
            DurationMs = durationMs,
            Success = success,
            Error = error,
            TraceContext = GetCurrentTraceContext()
        };
    }

    /// <summary>
    /// 记录消息诞生
    /// </summary>
    public Task RecordBornAsync(IMessage message, CancellationToken cancellationToken = default)
    {
        var tracePoint = CreateTracePoint(message, TracePointType.Born);
        return RecordAsync(tracePoint, cancellationToken);
    }

    /// <summary>
    /// 记录消息存储
    /// </summary>
    public Task RecordStoreAsync(
        IMessage message,
        int partition,
        long offset,
        long? durationMs = null,
        CancellationToken cancellationToken = default)
    {
        var tracePoint = CreateTracePoint(
            message,
            TracePointType.Store,
            partition: partition,
            offset: offset,
            durationMs: durationMs);
        return RecordAsync(tracePoint, cancellationToken);
    }

    /// <summary>
    /// 记录消息分发
    /// </summary>
    public Task RecordDispatchAsync(
        IMessage message,
        string consumerGroup,
        CancellationToken cancellationToken = default)
    {
        var tracePoint = CreateTracePoint(
            message,
            TracePointType.Dispatch,
            consumerGroup: consumerGroup);
        return RecordAsync(tracePoint, cancellationToken);
    }

    /// <summary>
    /// 记录消息消费
    /// </summary>
    public Task RecordConsumeAsync(
        IMessage message,
        string consumerGroup,
        long? durationMs = null,
        bool success = true,
        string? error = null,
        CancellationToken cancellationToken = default)
    {
        var tracePoint = CreateTracePoint(
            message,
            TracePointType.Consume,
            consumerGroup: consumerGroup,
            durationMs: durationMs,
            success: success,
            error: error);
        return RecordAsync(tracePoint, cancellationToken);
    }

    /// <summary>
    /// 记录消息确认
    /// </summary>
    public Task RecordAcknowledgeAsync(
        IMessage message,
        string consumerGroup,
        int partition,
        long offset,
        CancellationToken cancellationToken = default)
    {
        var tracePoint = CreateTracePoint(
            message,
            TracePointType.Acknowledge,
            consumerGroup: consumerGroup,
            partition: partition,
            offset: offset);
        return RecordAsync(tracePoint, cancellationToken);
    }

    /// <summary>
    /// 记录消息重试
    /// </summary>
    public Task RecordRetryAsync(
        IMessage message,
        string consumerGroup,
        int retryCount,
        string? error = null,
        CancellationToken cancellationToken = default)
    {
        var tracePoint = CreateTracePoint(
            message,
            TracePointType.Retry,
            consumerGroup: consumerGroup,
            success: false,
            error: error);
        tracePoint.Properties["RetryCount"] = retryCount.ToString();
        return RecordAsync(tracePoint, cancellationToken);
    }

    /// <summary>
    /// 记录死信
    /// </summary>
    public Task RecordDeadLetterAsync(
        IMessage message,
        string consumerGroup,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var tracePoint = CreateTracePoint(
            message,
            TracePointType.DeadLetter,
            consumerGroup: consumerGroup,
            success: false,
            error: reason);
        return RecordAsync(tracePoint, cancellationToken);
    }

    private static string GenerateTracePointId()
    {
        return $"TP{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():D13}{Random.Shared.Next(0, 99999):D5}";
    }

    private static TraceContext? GetCurrentTraceContext()
    {
        var activity = Activity.Current;
        if (activity == null)
        {
            return null;
        }
        
        return new TraceContext
        {
            TraceId = activity.TraceId.ToString(),
            SpanId = activity.SpanId.ToString(),
            ParentSpanId = activity.ParentSpanId.ToString(),
            Sampled = activity.Recorded
        };
    }
}
