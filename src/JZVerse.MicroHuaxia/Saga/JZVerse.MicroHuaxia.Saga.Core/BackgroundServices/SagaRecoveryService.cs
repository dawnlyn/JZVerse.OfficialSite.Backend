using JZVerse.MicroHuaxia.Saga.Abstractions;
using JZVerse.MicroHuaxia.Saga.Abstractions.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Saga.Core.BackgroundServices;

/// <summary>
/// Saga 恢复配置
/// </summary>
public sealed class SagaRecoveryOptions
{
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// 恢复检查间隔
    /// </summary>
    public TimeSpan RecoveryInterval { get; set; } = TimeSpan.FromMinutes(5);
    
    /// <summary>
    /// 最大恢复尝试次数（每个实例）
    /// </summary>
    public int MaxRecoveryAttempts { get; set; } = 3;
}

/// <summary>
/// Saga 故障恢复服务
/// </summary>
/// <remarks>
/// 负责检测并恢复中断的 Saga 实例：
/// - 检测状态为 Executing 或 Compensating 但长时间未更新的实例
/// - 尝试恢复执行或重新触发补偿
/// </remarks>
public sealed class SagaRecoveryService : BackgroundService
{
    private readonly ILogger<SagaRecoveryService> _logger;
    private readonly ISagaStore _sagaStore;
    private readonly ISagaOrchestrator _orchestrator;
    private readonly SagaRecoveryOptions _options;

    public SagaRecoveryService(
        ILogger<SagaRecoveryService> logger,
        ISagaStore sagaStore,
        ISagaOrchestrator orchestrator,
        SagaRecoveryOptions? options = null)
    {
        _logger = logger;
        _sagaStore = sagaStore;
        _orchestrator = orchestrator;
        _options = options ?? new SagaRecoveryOptions();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Saga recovery service is disabled");
            return;
        }
        
        _logger.LogInformation("Saga recovery service started with interval {Interval}", 
            _options.RecoveryInterval);
        
        // 启动时等待一段时间，让其他服务先初始化
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RecoverPendingSagasAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during saga recovery");
            }
            
            await Task.Delay(_options.RecoveryInterval, stoppingToken);
        }
        
        _logger.LogInformation("Saga recovery service stopped");
    }

    private async Task RecoverPendingSagasAsync(CancellationToken cancellationToken)
    {
        var pending = await _sagaStore.GetPendingInstancesAsync(cancellationToken);
        
        if (pending.Count == 0)
        {
            return;
        }
        
        _logger.LogInformation("Found {Count} pending saga instances to recover", pending.Count);
        
        foreach (var instance in pending)
        {
            try
            {
                await RecoverInstanceAsync(instance, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to recover saga {InstanceId}", instance.InstanceId);
            }
        }
    }

    private async Task RecoverInstanceAsync(SagaInstance instance, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Attempting to recover saga {InstanceId} from status {Status}", 
            instance.InstanceId, instance.Status);
        
        // 检查是否超过最大恢复次数
        // 这里简化处理，实际应该跟踪恢复次数
        
        var success = await _orchestrator.ResumeAsync(instance.InstanceId, cancellationToken);
        
        if (success)
        {
            _logger.LogInformation("Successfully recovered saga {InstanceId}", instance.InstanceId);
        }
        else
        {
            _logger.LogWarning("Failed to recover saga {InstanceId}", instance.InstanceId);
        }
    }
}
