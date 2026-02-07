using JZVerse.MicroHuaxia.Saga.Abstractions;
using JZVerse.MicroHuaxia.Saga.Abstractions.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Saga.Core.BackgroundServices;

/// <summary>
/// Saga 超时检测配置
/// </summary>
public sealed class SagaTimeoutOptions
{
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// 检查间隔
    /// </summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(1);
    
    /// <summary>
    /// 默认超时时间（用于没有设置超时的 Saga）
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(30);
}

/// <summary>
/// Saga 超时检测服务
/// </summary>
public sealed class SagaTimeoutService : BackgroundService
{
    private readonly ILogger<SagaTimeoutService> _logger;
    private readonly ISagaStore _sagaStore;
    private readonly SagaTimeoutOptions _options;

    public SagaTimeoutService(
        ILogger<SagaTimeoutService> logger,
        ISagaStore sagaStore,
        SagaTimeoutOptions? options = null)
    {
        _logger = logger;
        _sagaStore = sagaStore;
        _options = options ?? new SagaTimeoutOptions();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Saga timeout service is disabled");
            return;
        }
        
        _logger.LogInformation("Saga timeout service started with check interval {Interval}", 
            _options.CheckInterval);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckTimeoutsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during saga timeout check");
            }
            
            await Task.Delay(_options.CheckInterval, stoppingToken);
        }
        
        _logger.LogInformation("Saga timeout service stopped");
    }

    private async Task CheckTimeoutsAsync(CancellationToken cancellationToken)
    {
        var timedOut = await _sagaStore.GetTimeoutInstancesAsync(_options.DefaultTimeout, cancellationToken);
        
        foreach (var instance in timedOut)
        {
            // 检查实例是否真的超时（考虑实例自己的超时设置）
            var timeout = instance.Timeout ?? _options.DefaultTimeout;
            var elapsed = DateTimeOffset.UtcNow - (instance.StartedAt ?? instance.CreatedAt);
            
            if (elapsed > timeout)
            {
                _logger.LogWarning("Saga {InstanceId} has timed out after {Elapsed}", 
                    instance.InstanceId, elapsed);
                
                await _sagaStore.UpdateStatusAsync(
                    instance.InstanceId, 
                    SagaStatus.TimedOut,
                    $"Timed out after {elapsed}",
                    cancellationToken);
            }
        }
    }
}
