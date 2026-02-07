namespace JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;

/// <summary>
/// 消息信封（传输包装）
/// </summary>
public interface IMessageEnvelope
{
    /// <summary>
    /// 原始消息
    /// </summary>
    IMessage Message { get; }

    /// <summary>
    /// 投递次数
    /// </summary>
    int DeliveryCount { get; }

    /// <summary>
    /// 最后投递时间
    /// </summary>
    DateTimeOffset? LastDeliveryTime { get; }

    /// <summary>
    /// 链路追踪上下文
    /// </summary>
    TraceContext? TraceContext { get; }

    /// <summary>
    /// 路由信息
    /// </summary>
    RoutingInfo Routing { get; }

    /// <summary>
    /// 消息偏移量
    /// </summary>
    long Offset { get; }

    /// <summary>
    /// 分区号
    /// </summary>
    int Partition { get; }

    /// <summary>
    /// 消费者组
    /// </summary>
    string? ConsumerGroup { get; }
}

/// <summary>
/// 消息信封实现
/// </summary>
public sealed record MessageEnvelope : IMessageEnvelope
{
    /// <inheritdoc />
    public required IMessage Message { get; init; }

    /// <inheritdoc />
    public int DeliveryCount { get; init; } = 1;

    /// <inheritdoc />
    public DateTimeOffset? LastDeliveryTime { get; init; }

    /// <inheritdoc />
    public TraceContext? TraceContext { get; init; }

    /// <inheritdoc />
    public RoutingInfo Routing { get; init; } = new();

    /// <inheritdoc />
    public long Offset { get; init; }

    /// <inheritdoc />
    public int Partition { get; init; }

    /// <inheritdoc />
    public string? ConsumerGroup { get; init; }
}

/// <summary>
/// 链路追踪上下文
/// </summary>
public sealed record TraceContext
{
    /// <summary>
    /// 追踪ID
    /// </summary>
    public required string TraceId { get; init; }

    /// <summary>
    /// 跨度ID
    /// </summary>
    public required string SpanId { get; init; }

    /// <summary>
    /// 父跨度ID
    /// </summary>
    public string? ParentSpanId { get; init; }

    /// <summary>
    /// 采样标志
    /// </summary>
    public bool Sampled { get; init; } = true;

    /// <summary>
    /// 追踪标签
    /// </summary>
    public IDictionary<string, string> Tags { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// 路由信息
/// </summary>
public sealed record RoutingInfo
{
    /// <summary>
    /// 交换机名称
    /// </summary>
    public string? Exchange { get; init; }

    /// <summary>
    /// 路由键
    /// </summary>
    public string? RoutingKey { get; init; }

    /// <summary>
    /// 目标队列
    /// </summary>
    public string? Queue { get; init; }
}
