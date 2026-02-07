using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;

namespace JZVerse.MicroHuaxia.MessageQueue.Abstractions;

/// <summary>
/// 消费结果
/// </summary>
public enum ConsumeResult
{
    /// <summary>
    /// 消费成功
    /// </summary>
    Success = 0,

    /// <summary>
    /// 稍后重试
    /// </summary>
    RetryLater = 1,

    /// <summary>
    /// 消费失败，进入死信队列
    /// </summary>
    Failure = 2
}

/// <summary>
/// 消费选项
/// </summary>
public sealed record ConsumeOptions
{
    /// <summary>
    /// 消费者组
    /// </summary>
    public string? ConsumerGroup { get; init; }

    /// <summary>
    /// 消费模式
    /// </summary>
    public ConsumeMode Mode { get; init; } = ConsumeMode.Push;

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// 并发消费数
    /// </summary>
    public int Concurrency { get; init; } = 1;

    /// <summary>
    /// 批量拉取大小（Pull 模式）
    /// </summary>
    public int BatchSize { get; init; } = 10;

    /// <summary>
    /// 自动确认
    /// </summary>
    public bool AutoAck { get; init; } = false;

    /// <summary>
    /// 消费超时时间
    /// </summary>
    public TimeSpan ConsumeTimeout { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 消息过滤表达式（Tag 或 SQL92）
    /// </summary>
    public string? FilterExpression { get; init; }

    /// <summary>
    /// 过滤类型
    /// </summary>
    public FilterType FilterType { get; init; } = FilterType.Tag;

    /// <summary>
    /// 起始偏移量
    /// </summary>
    public OffsetReset OffsetReset { get; init; } = OffsetReset.Latest;
}

/// <summary>
/// 消费模式
/// </summary>
public enum ConsumeMode
{
    /// <summary>
    /// 推送模式（服务端推送）
    /// </summary>
    Push = 0,

    /// <summary>
    /// 拉取模式（客户端拉取）
    /// </summary>
    Pull = 1
}

/// <summary>
/// 过滤类型
/// </summary>
public enum FilterType
{
    /// <summary>
    /// 按 Tag 过滤
    /// </summary>
    Tag = 0,

    /// <summary>
    /// SQL92 表达式过滤
    /// </summary>
    Sql92 = 1
}

/// <summary>
/// 偏移量重置策略
/// </summary>
public enum OffsetReset
{
    /// <summary>
    /// 从最早消息开始
    /// </summary>
    Earliest = 0,

    /// <summary>
    /// 从最新消息开始
    /// </summary>
    Latest = 1
}

/// <summary>
/// 消息消费者接口
/// </summary>
public interface IMessageConsumer
{
    /// <summary>
    /// 订阅主题（Push 模式）
    /// </summary>
    /// <param name="topic">主题</param>
    /// <param name="handler">消息处理器</param>
    /// <param name="options">消费选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SubscribeAsync(
        string topic,
        Func<IMessageEnvelope, CancellationToken, Task<ConsumeResult>> handler,
        ConsumeOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取消订阅
    /// </summary>
    /// <param name="topic">主题</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UnsubscribeAsync(string topic, CancellationToken cancellationToken = default);

    /// <summary>
    /// 拉取消息（Pull 模式）
    /// </summary>
    /// <param name="topic">主题</param>
    /// <param name="batchSize">批量大小</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<IMessageEnvelope>> PullAsync(
        string topic,
        int batchSize = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 确认消息
    /// </summary>
    /// <param name="messageId">消息ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AcknowledgeAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 否认消息（触发重试）
    /// </summary>
    /// <param name="messageId">消息ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task NegativeAcknowledgeAsync(string messageId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 流消费者接口（高级）
/// </summary>
public interface IStreamConsumer
{
    /// <summary>
    /// 创建消息流
    /// </summary>
    /// <param name="topic">主题</param>
    /// <param name="options">消费选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    IAsyncEnumerable<IMessageEnvelope> CreateStreamAsync(
        string topic,
        ConsumeOptions? options = null,
        CancellationToken cancellationToken = default);
}
