using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Storage;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Transaction;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.MessageQueue.Core.Transaction;

/// <summary>
/// 事务消息生产者实现
/// </summary>
public sealed class TransactionProducer : ITransactionProducer
{
    private readonly ILogger<TransactionProducer> _logger;
    private readonly ITransactionStore _transactionStore;
    private readonly IMessageStore _messageStore;

    public TransactionProducer(
        ILogger<TransactionProducer> logger,
        ITransactionStore transactionStore,
        IMessageStore messageStore)
    {
        _logger = logger;
        _transactionStore = transactionStore;
        _messageStore = messageStore;
    }

    /// <inheritdoc />
    public async Task<TransactionResult> PrepareAsync(IMessage message, CancellationToken cancellationToken = default)
    {
        var transactionId = GenerateTransactionId();
        
        try
        {
            // 创建带有事务ID的消息副本
            var transactionalMessage = CloneMessageWithTransaction(message, transactionId);
            
            // 存储半消息
            await _transactionStore.StoreHalfMessageAsync(transactionId, transactionalMessage, cancellationToken);
            
            _logger.LogInformation(
                "Transaction {TransactionId} prepared, MessageId: {MessageId}, Topic: {Topic}",
                transactionId, message.MessageId, message.Topic);
            
            return TransactionResult.Ok(transactionId, transactionalMessage.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to prepare transaction for message {MessageId}", message.MessageId);
            return TransactionResult.Fail(ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task CommitAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Committing transaction {TransactionId}", transactionId);
        
        // 获取半消息
        var halfMessage = await _transactionStore.GetHalfMessageAsync(transactionId, cancellationToken);
        if (halfMessage is null)
        {
            throw new InvalidOperationException($"Transaction {transactionId} not found or already processed");
        }
        
        try
        {
            // 计算分区
            var partition = CalculatePartition(halfMessage);
            
            // 将消息追加到正式存储
            await _messageStore.AppendAsync(halfMessage, partition, cancellationToken);
            
            // 更新事务状态
            await _transactionStore.UpdateStatusAsync(transactionId, TransactionStatus.Committed, cancellationToken);
            
            // 删除半消息记录（可选：保留用于审计）
            // await _transactionStore.DeleteHalfMessageAsync(transactionId, cancellationToken);
            
            _logger.LogInformation(
                "Transaction {TransactionId} committed successfully, MessageId: {MessageId}",
                transactionId, halfMessage.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to commit transaction {TransactionId}", transactionId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task RollbackAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Rolling back transaction {TransactionId}", transactionId);
        
        try
        {
            // 更新事务状态
            await _transactionStore.UpdateStatusAsync(transactionId, TransactionStatus.RolledBack, cancellationToken);
            
            // 删除半消息
            await _transactionStore.DeleteHalfMessageAsync(transactionId, cancellationToken);
            
            _logger.LogInformation("Transaction {TransactionId} rolled back successfully", transactionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rollback transaction {TransactionId}", transactionId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<TransactionResult> ExecuteAsync(
        IMessage message,
        Func<IMessage, CancellationToken, Task<LocalTransactionResult>> localTransaction,
        CancellationToken cancellationToken = default)
    {
        // Phase 1: Prepare - 发送半消息
        var prepareResult = await PrepareAsync(message, cancellationToken);
        if (!prepareResult.Success)
        {
            return prepareResult;
        }
        
        var transactionId = prepareResult.TransactionId!;
        
        try
        {
            // 执行本地事务
            var halfMessage = await _transactionStore.GetHalfMessageAsync(transactionId, cancellationToken);
            var localResult = await localTransaction(halfMessage!, cancellationToken);
            
            // Phase 2: 根据本地事务结果提交或回滚
            switch (localResult)
            {
                case LocalTransactionResult.Commit:
                    await CommitAsync(transactionId, cancellationToken);
                    _logger.LogInformation(
                        "Transaction {TransactionId} completed with local commit",
                        transactionId);
                    return prepareResult;
                    
                case LocalTransactionResult.Rollback:
                    await RollbackAsync(transactionId, cancellationToken);
                    _logger.LogInformation(
                        "Transaction {TransactionId} completed with local rollback",
                        transactionId);
                    return TransactionResult.Fail("Local transaction requested rollback");
                    
                case LocalTransactionResult.Unknown:
                default:
                    // 保持半消息状态，等待回查
                    _logger.LogWarning(
                        "Transaction {TransactionId} local result unknown, will be checked later",
                        transactionId);
                    return prepareResult;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Local transaction failed for {TransactionId}, rolling back",
                transactionId);
            
            await RollbackAsync(transactionId, cancellationToken);
            return TransactionResult.Fail(ex.Message);
        }
    }

    private static string GenerateTransactionId()
    {
        // 格式: TXN + 时间戳(13位) + 随机数(8位)
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var random = Random.Shared.Next(0, 99999999);
        return $"TXN{timestamp:D13}{random:D8}";
    }

    private static IMessage CloneMessageWithTransaction(IMessage original, string transactionId)
    {
        return new Message
        {
            MessageId = original.MessageId,
            Topic = original.Topic,
            Tag = original.Tag,
            Body = original.Body,
            Headers = new Dictionary<string, string>(original.Headers),
            Timestamp = original.Timestamp,
            PartitionKey = original.PartitionKey,
            DelaySeconds = original.DelaySeconds,
            ExpireSeconds = original.ExpireSeconds,
            TransactionId = transactionId,
            Priority = original.Priority
        };
    }

    private static int CalculatePartition(IMessage message, int partitionCount = 4)
    {
        if (!string.IsNullOrEmpty(message.PartitionKey))
        {
            return Math.Abs(message.PartitionKey.GetHashCode()) % partitionCount;
        }
        return Random.Shared.Next(partitionCount);
    }
}
