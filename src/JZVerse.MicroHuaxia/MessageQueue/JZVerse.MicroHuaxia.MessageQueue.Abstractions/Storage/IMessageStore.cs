using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;

namespace JZVerse.MicroHuaxia.MessageQueue.Abstractions.Storage;

/// <summary>
/// 消息存储接口
/// </summary>
public interface IMessageStore
{
    /// <summary>
    /// 追加消息
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="partition">分区号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>消息偏移量</returns>
    Task<long> AppendAsync(IMessage message, int partition = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量追加消息
    /// </summary>
    /// <param name="messages">消息列表</param>
    /// <param name="partition">分区号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>起始偏移量</returns>
    Task<long> AppendBatchAsync(IEnumerable<IMessage> messages, int partition = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按ID获取消息
    /// </summary>
    /// <param name="messageId">消息ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IMessage?> GetByIdAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按偏移量获取消息
    /// </summary>
    /// <param name="topic">主题</param>
    /// <param name="partition">分区号</param>
    /// <param name="offset">偏移量</param>
    /// <param name="count">获取数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<IMessage>> GetByOffsetAsync(
        string topic,
        int partition,
        long offset,
        int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 按时间范围查询消息
    /// </summary>
    /// <param name="topic">主题</param>
    /// <param name="partition">分区号</param>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<IMessage>> GetByTimeRangeAsync(
        string topic,
        int partition,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除消息（逻辑删除）
    /// </summary>
    /// <param name="messageId">消息ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> DeleteAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取主题的最新偏移量
    /// </summary>
    /// <param name="topic">主题</param>
    /// <param name="partition">分区号</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<long> GetLatestOffsetAsync(string topic, int partition, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取主题的最早偏移量
    /// </summary>
    /// <param name="topic">主题</param>
    /// <param name="partition">分区号</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<long> GetEarliestOffsetAsync(string topic, int partition, CancellationToken cancellationToken = default);
}

/// <summary>
/// 偏移量管理器接口
/// </summary>
public interface IOffsetManager
{
    /// <summary>
    /// 获取消费位点
    /// </summary>
    /// <param name="consumerGroup">消费者组</param>
    /// <param name="topic">主题</param>
    /// <param name="partition">分区号</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<long> GetOffsetAsync(
        string consumerGroup,
        string topic,
        int partition,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 提交消费位点
    /// </summary>
    /// <param name="consumerGroup">消费者组</param>
    /// <param name="topic">主题</param>
    /// <param name="partition">分区号</param>
    /// <param name="offset">偏移量</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task CommitOffsetAsync(
        string consumerGroup,
        string topic,
        int partition,
        long offset,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 重置消费位点
    /// </summary>
    /// <param name="consumerGroup">消费者组</param>
    /// <param name="topic">主题</param>
    /// <param name="partition">分区号</param>
    /// <param name="offset">偏移量</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ResetOffsetAsync(
        string consumerGroup,
        string topic,
        int partition,
        long offset,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取消费延迟
    /// </summary>
    /// <param name="consumerGroup">消费者组</param>
    /// <param name="topic">主题</param>
    /// <param name="partition">分区号</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<long> GetLagAsync(
        string consumerGroup,
        string topic,
        int partition,
        CancellationToken cancellationToken = default);
}
