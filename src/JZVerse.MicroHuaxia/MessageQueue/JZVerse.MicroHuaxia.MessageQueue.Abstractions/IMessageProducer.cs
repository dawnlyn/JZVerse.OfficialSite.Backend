using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;

namespace JZVerse.MicroHuaxia.MessageQueue.Abstractions;

/// <summary>
/// 发送结果
/// </summary>
public sealed record SendResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 消息ID
    /// </summary>
    public string? MessageId { get; init; }

    /// <summary>
    /// 消息偏移量
    /// </summary>
    public long Offset { get; init; }

    /// <summary>
    /// 分区号
    /// </summary>
    public int Partition { get; init; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static SendResult Ok(string messageId, long offset, int partition) => new()
    {
        Success = true,
        MessageId = messageId,
        Offset = offset,
        Partition = partition
    };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static SendResult Fail(string error) => new()
    {
        Success = false,
        Error = error
    };
}

/// <summary>
/// 发送选项
/// </summary>
public sealed record SendOptions
{
    /// <summary>
    /// 超时时间
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; init; } = 3;

    /// <summary>
    /// 是否等待确认
    /// </summary>
    public bool WaitForAck { get; init; } = true;

    /// <summary>
    /// 确认模式
    /// </summary>
    public AckMode AckMode { get; init; } = AckMode.Leader;
}

/// <summary>
/// 确认模式
/// </summary>
public enum AckMode
{
    /// <summary>
    /// 无需确认（Fire and Forget）
    /// </summary>
    None = 0,

    /// <summary>
    /// Leader 确认即可
    /// </summary>
    Leader = 1,

    /// <summary>
    /// 所有副本确认
    /// </summary>
    All = 2
}

/// <summary>
/// 消息生产者接口
/// </summary>
public interface IMessageProducer
{
    /// <summary>
    /// 发送消息
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="options">发送选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<SendResult> SendAsync(
        IMessage message,
        SendOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量发送消息
    /// </summary>
    /// <param name="messages">消息列表</param>
    /// <param name="options">发送选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<SendResult>> SendBatchAsync(
        IEnumerable<IMessage> messages,
        SendOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送延迟消息
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="delay">延迟时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<SendResult> SendDelayedAsync(
        IMessage message,
        TimeSpan delay,
        CancellationToken cancellationToken = default);
}
