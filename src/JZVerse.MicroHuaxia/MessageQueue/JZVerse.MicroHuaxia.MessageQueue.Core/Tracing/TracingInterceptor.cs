using System.Diagnostics;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Storage;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Tracing;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.MessageQueue.Core.Tracing;

/// <summary>
/// 追踪消息存储装饰器
/// 包装 IMessageStore，自动记录存储相关的追踪点
/// </summary>
public sealed class TracingMessageStoreDecorator : IMessageStore
{
    private readonly IMessageStore _inner;
    private readonly MessageTracer _tracer;
    private readonly ILogger<TracingMessageStoreDecorator> _logger;

    public TracingMessageStoreDecorator(
        IMessageStore inner,
        MessageTracer tracer,
        ILogger<TracingMessageStoreDecorator> logger)
    {
        _inner = inner;
        _tracer = tracer;
        _logger = logger;
    }

    public async Task<long> AppendAsync(IMessage message, int partition = 0, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        
        try
        {
            // 记录消息诞生（如果还没有记录）
            await _tracer.RecordBornAsync(message, cancellationToken);
            
            var offset = await _inner.AppendAsync(message, partition, cancellationToken);
            
            sw.Stop();
            
            // 记录存储成功
            await _tracer.RecordStoreAsync(message, partition, offset, sw.ElapsedMilliseconds, cancellationToken);
            
            return offset;
        }
        catch (Exception ex)
        {
            sw.Stop();
            
            // 记录存储失败
            var tracePoint = _tracer.CreateTracePoint(
                message,
                TracePointType.Store,
                partition: partition,
                durationMs: sw.ElapsedMilliseconds,
                success: false,
                error: ex.Message);
            await _tracer.RecordAsync(tracePoint, cancellationToken);
            
            throw;
        }
    }

    public async Task<long> AppendBatchAsync(IEnumerable<IMessage> messages, int partition = 0, CancellationToken cancellationToken = default)
    {
        var messageList = messages.ToList();
        var sw = Stopwatch.StartNew();
        
        try
        {
            // 记录批量消息诞生
            foreach (var message in messageList)
            {
                await _tracer.RecordBornAsync(message, cancellationToken);
            }
            
            var firstOffset = await _inner.AppendBatchAsync(messageList, partition, cancellationToken);
            
            sw.Stop();
            var durationPerMessage = messageList.Count > 0 ? sw.ElapsedMilliseconds / messageList.Count : 0;
            
            // 记录存储成功
            var offset = firstOffset;
            foreach (var message in messageList)
            {
                await _tracer.RecordStoreAsync(message, partition, offset++, durationPerMessage, cancellationToken);
            }
            
            return firstOffset;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Failed to append batch of {Count} messages", messageList.Count);
            throw;
        }
    }

    public Task<IMessage?> GetByIdAsync(string messageId, CancellationToken cancellationToken = default)
        => _inner.GetByIdAsync(messageId, cancellationToken);

    public Task<IReadOnlyList<IMessage>> GetByOffsetAsync(string topic, int partition, long offset, int count, CancellationToken cancellationToken = default)
        => _inner.GetByOffsetAsync(topic, partition, offset, count, cancellationToken);

    public Task<IReadOnlyList<IMessage>> GetByTimeRangeAsync(string topic, int partition, DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken cancellationToken = default)
        => _inner.GetByTimeRangeAsync(topic, partition, startTime, endTime, cancellationToken);

    public Task<bool> DeleteAsync(string messageId, CancellationToken cancellationToken = default)
        => _inner.DeleteAsync(messageId, cancellationToken);

    public Task<long> GetLatestOffsetAsync(string topic, int partition, CancellationToken cancellationToken = default)
        => _inner.GetLatestOffsetAsync(topic, partition, cancellationToken);

    public Task<long> GetEarliestOffsetAsync(string topic, int partition, CancellationToken cancellationToken = default)
        => _inner.GetEarliestOffsetAsync(topic, partition, cancellationToken);
}

/// <summary>
/// 追踪消息消费拦截器
/// </summary>
public sealed class TracingConsumeInterceptor
{
    private readonly MessageTracer _tracer;
    private readonly ILogger<TracingConsumeInterceptor> _logger;

    public TracingConsumeInterceptor(
        MessageTracer tracer,
        ILogger<TracingConsumeInterceptor> logger)
    {
        _tracer = tracer;
        _logger = logger;
    }

    /// <summary>
    /// 包装消息处理器，自动记录消费追踪
    /// </summary>
    public Func<IMessageEnvelope, CancellationToken, Task> Wrap(
        Func<IMessageEnvelope, CancellationToken, Task> handler,
        string consumerGroup)
    {
        return async (envelope, ct) =>
        {
            var sw = Stopwatch.StartNew();
            var message = envelope.Message;
            
            // 记录分发
            await _tracer.RecordDispatchAsync(message, consumerGroup, ct);
            
            try
            {
                await handler(envelope, ct);
                
                sw.Stop();
                
                // 记录消费成功
                await _tracer.RecordConsumeAsync(
                    message,
                    consumerGroup,
                    sw.ElapsedMilliseconds,
                    success: true,
                    cancellationToken: ct);
            }
            catch (Exception ex)
            {
                sw.Stop();
                
                // 记录消费失败
                await _tracer.RecordConsumeAsync(
                    message,
                    consumerGroup,
                    sw.ElapsedMilliseconds,
                    success: false,
                    error: ex.Message,
                    cancellationToken: ct);
                
                throw;
            }
        };
    }

    /// <summary>
    /// 记录消息确认
    /// </summary>
    public Task RecordAckAsync(
        IMessage message,
        string consumerGroup,
        int partition,
        long offset,
        CancellationToken cancellationToken = default)
    {
        return _tracer.RecordAcknowledgeAsync(message, consumerGroup, partition, offset, cancellationToken);
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
        return _tracer.RecordRetryAsync(message, consumerGroup, retryCount, error, cancellationToken);
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
        return _tracer.RecordDeadLetterAsync(message, consumerGroup, reason, cancellationToken);
    }
}

/// <summary>
/// 追踪上下文持有者（用于在调用链中传递追踪信息）
/// </summary>
public static class TraceContextHolder
{
    private static readonly AsyncLocal<TraceContext?> _current = new();

    /// <summary>
    /// 当前追踪上下文
    /// </summary>
    public static TraceContext? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }

    /// <summary>
    /// 创建新的追踪上下文
    /// </summary>
    public static TraceContext CreateNew(string? parentTraceId = null, string? parentSpanId = null)
    {
        return new TraceContext
        {
            TraceId = parentTraceId ?? GenerateTraceId(),
            SpanId = GenerateSpanId(),
            ParentSpanId = parentSpanId,
            Sampled = true
        };
    }

    /// <summary>
    /// 从消息头中提取追踪上下文
    /// </summary>
    public static TraceContext? ExtractFromHeaders(IDictionary<string, string> headers)
    {
        if (!headers.TryGetValue("X-Trace-Id", out var traceId))
        {
            return null;
        }

        headers.TryGetValue("X-Span-Id", out var spanId);
        headers.TryGetValue("X-Parent-Span-Id", out var parentSpanId);
        headers.TryGetValue("X-Sampled", out var sampledStr);

        return new TraceContext
        {
            TraceId = traceId,
            SpanId = spanId ?? GenerateSpanId(),
            ParentSpanId = parentSpanId,
            Sampled = sampledStr != "0"
        };
    }

    /// <summary>
    /// 将追踪上下文注入到消息头
    /// </summary>
    public static void InjectToHeaders(TraceContext context, IDictionary<string, string> headers)
    {
        headers["X-Trace-Id"] = context.TraceId;
        headers["X-Span-Id"] = context.SpanId;
        if (context.ParentSpanId != null)
        {
            headers["X-Parent-Span-Id"] = context.ParentSpanId;
        }
        headers["X-Sampled"] = context.Sampled ? "1" : "0";
    }

    private static string GenerateTraceId()
    {
        return Guid.NewGuid().ToString("N");
    }

    private static string GenerateSpanId()
    {
        return Guid.NewGuid().ToString("N")[..16];
    }
}
