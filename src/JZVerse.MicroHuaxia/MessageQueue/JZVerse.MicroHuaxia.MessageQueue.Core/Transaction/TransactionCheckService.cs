using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Transaction;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.MessageQueue.Core.Transaction;

/// <summary>
/// 事务回查服务配置
/// </summary>
public sealed class TransactionCheckOptions
{
    /// <summary>
    /// 回查间隔（默认60秒）
    /// </summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(60);
    
    /// <summary>
    /// 事务超时时间（超过此时间触发回查，默认30秒）
    /// </summary>
    public TimeSpan TransactionTimeout { get; set; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// 最大回查次数（默认15次）
    /// </summary>
    public int MaxCheckCount { get; set; } = 15;
    
    /// <summary>
    /// 是否启用回查服务
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// 事务回查后台服务
/// 定期检查处于 Preparing 状态的半消息，向生产者回查事务状态
/// </summary>
public sealed class TransactionCheckService : BackgroundService
{
    private readonly ILogger<TransactionCheckService> _logger;
    private readonly ITransactionStore _transactionStore;
    private readonly ITransactionProducer _transactionProducer;
    private readonly TransactionCheckOptions _options;
    private readonly ITransactionCheckListener? _checkListener;

    public TransactionCheckService(
        ILogger<TransactionCheckService> logger,
        ITransactionStore transactionStore,
        ITransactionProducer transactionProducer,
        TransactionCheckOptions options,
        ITransactionCheckListener? checkListener = null)
    {
        _logger = logger;
        _transactionStore = transactionStore;
        _transactionProducer = transactionProducer;
        _options = options;
        _checkListener = checkListener;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Transaction check service is disabled");
            return;
        }
        
        _logger.LogInformation(
            "Transaction check service started. CheckInterval: {Interval}s, Timeout: {Timeout}s, MaxCheckCount: {MaxCheck}",
            _options.CheckInterval.TotalSeconds,
            _options.TransactionTimeout.TotalSeconds,
            _options.MaxCheckCount);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckPendingTransactionsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during transaction check");
            }
            
            await Task.Delay(_options.CheckInterval, stoppingToken);
        }
        
        _logger.LogInformation("Transaction check service stopped");
    }

    private async Task CheckPendingTransactionsAsync(CancellationToken cancellationToken)
    {
        var pendingTransactions = await _transactionStore.GetPendingTransactionsAsync(
            _options.TransactionTimeout,
            cancellationToken);
        
        if (pendingTransactions.Count == 0)
        {
            return;
        }
        
        _logger.LogInformation("Found {Count} pending transactions to check", pendingTransactions.Count);
        
        foreach (var (transactionId, message) in pendingTransactions)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            
            await CheckTransactionAsync(transactionId, message, cancellationToken);
        }
    }

    private async Task CheckTransactionAsync(string transactionId, Abstractions.Models.IMessage message, CancellationToken cancellationToken)
    {
        try
        {
            // 获取回查次数
            var checkCount = GetCheckCount(transactionId);
            
            if (checkCount >= _options.MaxCheckCount)
            {
                _logger.LogWarning(
                    "Transaction {TransactionId} exceeded max check count ({MaxCount}), rolling back",
                    transactionId, _options.MaxCheckCount);
                
                await _transactionProducer.RollbackAsync(transactionId, cancellationToken);
                return;
            }
            
            // 增加回查次数
            IncrementCheckCount(transactionId);
            
            // 如果有回查监听器，执行回查
            if (_checkListener is not null)
            {
                _logger.LogDebug(
                    "Checking transaction {TransactionId}, attempt {Attempt}/{MaxAttempts}",
                    transactionId, checkCount + 1, _options.MaxCheckCount);
                
                var result = await _checkListener.CheckLocalTransactionAsync(message, cancellationToken);
                
                switch (result)
                {
                    case LocalTransactionResult.Commit:
                        _logger.LogInformation(
                            "Transaction {TransactionId} check result: Commit",
                            transactionId);
                        await _transactionProducer.CommitAsync(transactionId, cancellationToken);
                        break;
                        
                    case LocalTransactionResult.Rollback:
                        _logger.LogInformation(
                            "Transaction {TransactionId} check result: Rollback",
                            transactionId);
                        await _transactionProducer.RollbackAsync(transactionId, cancellationToken);
                        break;
                        
                    case LocalTransactionResult.Unknown:
                    default:
                        _logger.LogDebug(
                            "Transaction {TransactionId} check result: Unknown, will retry",
                            transactionId);
                        break;
                }
            }
            else
            {
                _logger.LogWarning(
                    "No transaction check listener configured, cannot check transaction {TransactionId}",
                    transactionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking transaction {TransactionId}", transactionId);
        }
    }

    private int GetCheckCount(string transactionId)
    {
        if (_transactionStore is MemoryTransactionStore memoryStore)
        {
            return memoryStore.GetCheckCount(transactionId);
        }
        return 0;
    }

    private void IncrementCheckCount(string transactionId)
    {
        if (_transactionStore is MemoryTransactionStore memoryStore)
        {
            memoryStore.IncrementCheckCount(transactionId);
        }
    }
}
