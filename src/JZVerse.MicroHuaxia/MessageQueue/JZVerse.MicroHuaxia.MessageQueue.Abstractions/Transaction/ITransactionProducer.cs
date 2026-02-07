using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;

namespace JZVerse.MicroHuaxia.MessageQueue.Abstractions.Transaction;

/// <summary>
/// 事务状态
/// </summary>
public enum TransactionStatus
{
    /// <summary>
    /// 未知状态
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 准备中（半消息）
    /// </summary>
    Preparing = 1,

    /// <summary>
    /// 已提交
    /// </summary>
    Committed = 2,

    /// <summary>
    /// 已回滚
    /// </summary>
    RolledBack = 3
}

/// <summary>
/// 事务结果
/// </summary>
public sealed record TransactionResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 事务ID
    /// </summary>
    public string? TransactionId { get; init; }

    /// <summary>
    /// 消息ID
    /// </summary>
    public string? MessageId { get; init; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static TransactionResult Ok(string transactionId, string messageId) => new()
    {
        Success = true,
        TransactionId = transactionId,
        MessageId = messageId
    };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static TransactionResult Fail(string error) => new()
    {
        Success = false,
        Error = error
    };
}

/// <summary>
/// 本地事务执行结果
/// </summary>
public enum LocalTransactionResult
{
    /// <summary>
    /// 提交事务
    /// </summary>
    Commit = 0,

    /// <summary>
    /// 回滚事务
    /// </summary>
    Rollback = 1,

    /// <summary>
    /// 未知，需要回查
    /// </summary>
    Unknown = 2
}

/// <summary>
/// 事务消息生产者接口
/// </summary>
public interface ITransactionProducer
{
    /// <summary>
    /// 发送半消息（准备阶段）
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<TransactionResult> PrepareAsync(IMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 提交事务
    /// </summary>
    /// <param name="transactionId">事务ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task CommitAsync(string transactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 回滚事务
    /// </summary>
    /// <param name="transactionId">事务ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RollbackAsync(string transactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行事务消息（自动处理准备和提交/回滚）
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="localTransaction">本地事务执行器</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<TransactionResult> ExecuteAsync(
        IMessage message,
        Func<IMessage, CancellationToken, Task<LocalTransactionResult>> localTransaction,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 事务回查监听器接口
/// </summary>
public interface ITransactionCheckListener
{
    /// <summary>
    /// 检查本地事务状态
    /// </summary>
    /// <param name="message">半消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>本地事务执行结果</returns>
    Task<LocalTransactionResult> CheckLocalTransactionAsync(IMessage message, CancellationToken cancellationToken = default);
}

/// <summary>
/// 事务存储接口
/// </summary>
public interface ITransactionStore
{
    /// <summary>
    /// 存储半消息
    /// </summary>
    /// <param name="transactionId">事务ID</param>
    /// <param name="message">消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task StoreHalfMessageAsync(string transactionId, IMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取半消息
    /// </summary>
    /// <param name="transactionId">事务ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IMessage?> GetHalfMessageAsync(string transactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除半消息
    /// </summary>
    /// <param name="transactionId">事务ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DeleteHalfMessageAsync(string transactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新事务状态
    /// </summary>
    /// <param name="transactionId">事务ID</param>
    /// <param name="status">事务状态</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateStatusAsync(string transactionId, TransactionStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取待回查的事务列表
    /// </summary>
    /// <param name="timeout">超时时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<(string TransactionId, IMessage Message)>> GetPendingTransactionsAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
