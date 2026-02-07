using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;

namespace JZVerse.MicroHuaxia.MessageQueue.Abstractions.Tracing;

/// <summary>
/// 追踪点类型
/// </summary>
public enum TracePointType
{
    /// <summary>
    /// 消息诞生（生产者端）
    /// </summary>
    Born = 0,

    /// <summary>
    /// 消息存储
    /// </summary>
    Store = 1,

    /// <summary>
    /// 消息分发
    /// </summary>
    Dispatch = 2,

    /// <summary>
    /// 消息消费
    /// </summary>
    Consume = 3,

    /// <summary>
    /// 消息确认
    /// </summary>
    Acknowledge = 4,

    /// <summary>
    /// 消息重试
    /// </summary>
    Retry = 5,

    /// <summary>
    /// 进入死信队列
    /// </summary>
    DeadLetter = 6
}

/// <summary>
/// 消息追踪点
/// </summary>
public sealed record MessageTracePoint
{
    /// <summary>
    /// 追踪点ID
    /// </summary>
    public required string TracePointId { get; init; }

    /// <summary>
    /// 消息ID
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// 追踪点类型
    /// </summary>
    public TracePointType Type { get; init; }

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 节点标识（生产者/Broker/消费者）
    /// </summary>
    public required string NodeId { get; init; }

    /// <summary>
    /// 主题
    /// </summary>
    public string? Topic { get; init; }

    /// <summary>
    /// 消费者组
    /// </summary>
    public string? ConsumerGroup { get; init; }

    /// <summary>
    /// 分区号
    /// </summary>
    public int? Partition { get; init; }

    /// <summary>
    /// 偏移量
    /// </summary>
    public long? Offset { get; init; }

    /// <summary>
    /// 耗时（毫秒）
    /// </summary>
    public long? DurationMs { get; init; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; init; } = true;

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// 追踪上下文
    /// </summary>
    public TraceContext? TraceContext { get; init; }

    /// <summary>
    /// 附加属性
    /// </summary>
    public IDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// 消息完整轨迹
/// </summary>
public sealed record MessageTrace
{
    /// <summary>
    /// 消息ID
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// 追踪点列表（按时间排序）
    /// </summary>
    public IReadOnlyList<MessageTracePoint> TracePoints { get; init; } = [];

    /// <summary>
    /// 消息诞生时间
    /// </summary>
    public DateTimeOffset? BornTime => TracePoints.FirstOrDefault(p => p.Type == TracePointType.Born)?.Timestamp;

    /// <summary>
    /// 消息存储时间
    /// </summary>
    public DateTimeOffset? StoreTime => TracePoints.FirstOrDefault(p => p.Type == TracePointType.Store)?.Timestamp;

    /// <summary>
    /// 消息消费时间
    /// </summary>
    public DateTimeOffset? ConsumeTime => TracePoints.FirstOrDefault(p => p.Type == TracePointType.Consume)?.Timestamp;

    /// <summary>
    /// 端到端延迟（毫秒）
    /// </summary>
    public long? EndToEndLatencyMs => BornTime.HasValue && ConsumeTime.HasValue
        ? (long)(ConsumeTime.Value - BornTime.Value).TotalMilliseconds
        : null;

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount => TracePoints.Count(p => p.Type == TracePointType.Retry);

    /// <summary>
    /// 是否已消费成功
    /// </summary>
    public bool IsConsumed => TracePoints.Any(p => p.Type == TracePointType.Acknowledge && p.Success);

    /// <summary>
    /// 是否进入死信队列
    /// </summary>
    public bool IsDeadLettered => TracePoints.Any(p => p.Type == TracePointType.DeadLetter);
}

/// <summary>
/// 消息追踪服务接口
/// </summary>
public interface IMessageTracer
{
    /// <summary>
    /// 记录追踪点
    /// </summary>
    /// <param name="tracePoint">追踪点</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RecordAsync(MessageTracePoint tracePoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询消息轨迹
    /// </summary>
    /// <param name="messageId">消息ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<MessageTrace?> GetTraceAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按主题查询追踪点
    /// </summary>
    /// <param name="topic">主题</param>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<MessageTracePoint>> QueryByTopicAsync(
        string topic,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 按消费者组查询追踪点
    /// </summary>
    /// <param name="consumerGroup">消费者组</param>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<MessageTracePoint>> QueryByConsumerGroupAsync(
        string consumerGroup,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 消息追踪存储接口
/// </summary>
public interface IMessageTraceStore
{
    /// <summary>
    /// 存储追踪点
    /// </summary>
    Task StoreAsync(MessageTracePoint tracePoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按消息ID查询追踪点
    /// </summary>
    Task<IReadOnlyList<MessageTracePoint>> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按时间范围查询
    /// </summary>
    Task<IReadOnlyList<MessageTracePoint>> QueryAsync(
        string? topic,
        string? consumerGroup,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        int limit = 1000,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 清理过期追踪数据
    /// </summary>
    Task CleanupAsync(TimeSpan retention, CancellationToken cancellationToken = default);
}
