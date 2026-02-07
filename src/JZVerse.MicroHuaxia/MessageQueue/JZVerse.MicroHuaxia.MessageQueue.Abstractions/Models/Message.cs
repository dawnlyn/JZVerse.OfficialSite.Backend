namespace JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;

/// <summary>
/// 消息接口
/// </summary>
public interface IMessage
{
    /// <summary>
    /// 全局唯一消息ID
    /// </summary>
    string MessageId { get; }

    /// <summary>
    /// 主题名称
    /// </summary>
    string Topic { get; }

    /// <summary>
    /// 消息标签（用于过滤）
    /// </summary>
    string? Tag { get; }

    /// <summary>
    /// 消息体
    /// </summary>
    byte[] Body { get; }

    /// <summary>
    /// 消息头（元数据）
    /// </summary>
    IDictionary<string, string> Headers { get; }

    /// <summary>
    /// 生产时间戳
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// 分区键（用于顺序保证）
    /// </summary>
    string? PartitionKey { get; }

    /// <summary>
    /// 延迟投递时间（秒）
    /// </summary>
    int? DelaySeconds { get; }

    /// <summary>
    /// 消息过期时间（秒）
    /// </summary>
    int? ExpireSeconds { get; }

    /// <summary>
    /// 事务ID（用于事务消息）
    /// </summary>
    string? TransactionId { get; }

    /// <summary>
    /// 消息优先级（0-9，数字越大优先级越高）
    /// </summary>
    int Priority { get; }
}

/// <summary>
/// 消息实现
/// </summary>
public sealed record Message : IMessage
{
    /// <inheritdoc />
    public required string MessageId { get; init; }

    /// <inheritdoc />
    public required string Topic { get; init; }

    /// <inheritdoc />
    public string? Tag { get; init; }

    /// <inheritdoc />
    public required byte[] Body { get; init; }

    /// <inheritdoc />
    public IDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public string? PartitionKey { get; init; }

    /// <inheritdoc />
    public int? DelaySeconds { get; init; }

    /// <inheritdoc />
    public int? ExpireSeconds { get; init; }

    /// <inheritdoc />
    public string? TransactionId { get; init; }

    /// <inheritdoc />
    public int Priority { get; init; } = 5;
}
