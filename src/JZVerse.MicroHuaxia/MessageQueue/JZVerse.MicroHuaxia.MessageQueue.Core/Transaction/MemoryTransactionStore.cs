using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Transaction;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.MessageQueue.Core.Transaction;

/// <summary>
/// 事务记录
/// </summary>
internal sealed record TransactionRecord
{
    /// <summary>
    /// 事务ID
    /// </summary>
    public required string TransactionId { get; init; }
    
    /// <summary>
    /// 半消息
    /// </summary>
    public required IMessage Message { get; init; }
    
    /// <summary>
    /// 事务状态
    /// </summary>
    public TransactionStatus Status { get; set; }
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// 回查次数
    /// </summary>
    public int CheckCount { get; set; }
}

/// <summary>
/// 内存事务存储实现
/// </summary>
public sealed class MemoryTransactionStore : ITransactionStore, IDisposable
{
    private readonly ILogger<MemoryTransactionStore> _logger;
    private readonly ConcurrentDictionary<string, TransactionRecord> _transactions = new();
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _expireTime = TimeSpan.FromHours(24);
    private bool _disposed;

    public MemoryTransactionStore(ILogger<MemoryTransactionStore> logger)
    {
        _logger = logger;
        
        // 每小时清理过期事务
        _cleanupTimer = new Timer(CleanupExpiredTransactions, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
    }

    /// <inheritdoc />
    public Task StoreHalfMessageAsync(string transactionId, IMessage message, CancellationToken cancellationToken = default)
    {
        var record = new TransactionRecord
        {
            TransactionId = transactionId,
            Message = message,
            Status = TransactionStatus.Preparing
        };
        
        if (!_transactions.TryAdd(transactionId, record))
        {
            throw new InvalidOperationException($"Transaction {transactionId} already exists");
        }
        
        _logger.LogDebug("Stored half message for transaction {TransactionId}, MessageId: {MessageId}", 
            transactionId, message.MessageId);
        
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IMessage?> GetHalfMessageAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        if (_transactions.TryGetValue(transactionId, out var record))
        {
            return Task.FromResult<IMessage?>(record.Message);
        }
        
        return Task.FromResult<IMessage?>(null);
    }

    /// <inheritdoc />
    public Task DeleteHalfMessageAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        if (_transactions.TryRemove(transactionId, out var record))
        {
            _logger.LogDebug("Deleted half message for transaction {TransactionId}", transactionId);
        }
        
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateStatusAsync(string transactionId, TransactionStatus status, CancellationToken cancellationToken = default)
    {
        if (_transactions.TryGetValue(transactionId, out var record))
        {
            record.Status = status;
            record.UpdatedAt = DateTimeOffset.UtcNow;
            
            _logger.LogDebug("Updated transaction {TransactionId} status to {Status}", transactionId, status);
        }
        else
        {
            _logger.LogWarning("Transaction {TransactionId} not found for status update", transactionId);
        }
        
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<(string TransactionId, IMessage Message)>> GetPendingTransactionsAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var cutoffTime = DateTimeOffset.UtcNow - timeout;
        
        var pendingTransactions = _transactions.Values
            .Where(r => r.Status == TransactionStatus.Preparing && r.CreatedAt < cutoffTime)
            .Select(r => (r.TransactionId, r.Message))
            .ToList();
        
        return Task.FromResult<IReadOnlyList<(string TransactionId, IMessage Message)>>(pendingTransactions);
    }

    /// <summary>
    /// 增加回查次数
    /// </summary>
    public void IncrementCheckCount(string transactionId)
    {
        if (_transactions.TryGetValue(transactionId, out var record))
        {
            record.CheckCount++;
            record.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// 获取回查次数
    /// </summary>
    public int GetCheckCount(string transactionId)
    {
        if (_transactions.TryGetValue(transactionId, out var record))
        {
            return record.CheckCount;
        }
        return 0;
    }

    /// <summary>
    /// 获取事务状态
    /// </summary>
    public TransactionStatus? GetStatus(string transactionId)
    {
        if (_transactions.TryGetValue(transactionId, out var record))
        {
            return record.Status;
        }
        return null;
    }

    private void CleanupExpiredTransactions(object? state)
    {
        if (_disposed) return;
        
        var cutoffTime = DateTimeOffset.UtcNow - _expireTime;
        var expiredKeys = _transactions
            .Where(kvp => kvp.Value.Status != TransactionStatus.Preparing && kvp.Value.UpdatedAt < cutoffTime)
            .Select(kvp => kvp.Key)
            .ToList();
        
        foreach (var key in expiredKeys)
        {
            _transactions.TryRemove(key, out _);
        }
        
        if (expiredKeys.Count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired transactions", expiredKeys.Count);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _cleanupTimer.Dispose();
        _transactions.Clear();
    }
}
