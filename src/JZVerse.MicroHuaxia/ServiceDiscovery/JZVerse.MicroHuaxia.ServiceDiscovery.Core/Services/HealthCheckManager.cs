using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Core.Services;

/// <summary>
/// 健康检查管理器实现
/// </summary>
public class HealthCheckManager(
    IServiceInstanceRepository _repository,
    IServiceRegistry _registry,
    IServiceDiscoveryCache _cache,
    ILogger<HealthCheckManager> _logger,
    int _checkIntervalSeconds = 10,
    int _heartbeatTimeoutSeconds = 30,
    int _failureThreshold = 3
) : IHealthCheckManager, IDisposable
{
    private readonly List<IHealthChecker> _checkers = [];

    private PeriodicTimer? _timer;
    private Task? _checkTask;
    private CancellationTokenSource? _cts;

    /// <inheritdoc />
    public void RegisterChecker(IHealthChecker checker)
    {
        _checkers.Add(checker);
        _logger.LogInformation("健康检查器已注册: {CheckerName}", checker.Name);
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _timer = new(TimeSpan.FromSeconds(_checkIntervalSeconds));
        _checkTask = RunHealthCheckLoopAsync(_cts.Token);

        _logger.LogInformation(
            "Health check manager started with interval: {Interval}s, timeout: {Timeout}s",
            _checkIntervalSeconds,
            _heartbeatTimeoutSeconds
        );

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts != null)
        {
            await _cts.CancelAsync();
            if (_checkTask != null)
            {
                try
                {
                    await _checkTask;
                }
                catch (OperationCanceledException)
                {
                    // 预期的取消
                }
            }
        }

        _timer?.Dispose();
        _logger.LogInformation("Health check manager stopped");
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken = default
    )
    {
        var instance = await _repository.GetByIdAsync(instanceId, cancellationToken);
        if (instance == null)
        {
            return HealthCheckResult.Unhealthy($"Instance '{instanceId}' not found");
        }

        return await PerformHealthCheckAsync(instance, cancellationToken);
    }

    private async Task RunHealthCheckLoopAsync(CancellationToken cancellationToken)
    {
        while (_timer != null && await _timer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                await PerformAllHealthChecksAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during health check loop");
            }
        }
    }

    private async Task PerformAllHealthChecksAsync(CancellationToken cancellationToken)
    {
        var instances = await _repository.GetAllAsync(cancellationToken);

        _logger.LogDebug("Performing health checks for {Count} instances", instances.Count);

        var tasks = instances
            .Where(i => i.Enabled)
            .Select(instance => CheckAndUpdateInstanceHealthAsync(instance, cancellationToken));

        await Task.WhenAll(tasks);
    }

    private async Task CheckAndUpdateInstanceHealthAsync(ServiceInstance instance, CancellationToken cancellationToken)
    {
        try
        {
            // 检查心跳超时
            var heartbeatAge = DateTimeOffset.UtcNow - instance.LastHeartbeatAt;
            if (heartbeatAge > TimeSpan.FromSeconds(_heartbeatTimeoutSeconds))
            {
                _logger.LogWarning(
                    "Instance {ServiceName}:{InstanceId} heartbeat timeout (last: {LastHeartbeat})",
                    instance.ServiceName,
                    instance.InstanceId,
                    instance.LastHeartbeatAt
                );

                await MarkAsUnhealthyAsync(instance, "Heartbeat timeout", cancellationToken);
                return;
            }

            // 执行主动健康检查
            var result = await PerformHealthCheckAsync(instance, cancellationToken);

            // 更新健康状态
            if (result.Status == HealthStatus.Healthy)
            {
                if (instance.Health != HealthStatus.Healthy)
                {
                    await _registry.UpdateHealthStatusAsync(
                        instance.InstanceId,
                        HealthStatus.Healthy,
                        cancellationToken
                    );

                    // 清除缓存以便下次获取新数据
                    await _cache.ClearCacheAsync(instance.ServiceName, cancellationToken);
                }
            }
            else
            {
                instance.FailureCount++;

                // 达到失败阈值，标记为不健康
                if (instance.FailureCount >= _failureThreshold)
                {
                    await MarkAsUnhealthyAsync(instance, result.Message, cancellationToken);
                }
                else
                {
                    _logger.LogDebug(
                        "Instance {ServiceName}:{InstanceId} health check failed ({FailureCount}/{Threshold})",
                        instance.ServiceName,
                        instance.InstanceId,
                        instance.FailureCount,
                        _failureThreshold
                    );
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error checking health for {ServiceName}:{InstanceId}",
                instance.ServiceName,
                instance.InstanceId
            );
        }
    }

    private async Task<HealthCheckResult> PerformHealthCheckAsync(
        ServiceInstance instance,
        CancellationToken cancellationToken
    )
    {
        var checker = _checkers.FirstOrDefault(c => c.CanCheck(instance));

        if (checker == null)
        {
            _logger.LogDebug(
                "No suitable health checker found for {ServiceName}:{InstanceId}",
                instance.ServiceName,
                instance.InstanceId
            );

            return new() { Status = HealthStatus.Unknown, Message = "No suitable health checker found" };
        }

        return await checker.CheckHealthAsync(instance, cancellationToken);
    }

    private async Task MarkAsUnhealthyAsync(
        ServiceInstance instance,
        string? reason,
        CancellationToken cancellationToken
    )
    {
        if (instance.Health != HealthStatus.Unhealthy)
        {
            _logger.LogWarning(
                "Marking instance as unhealthy: {ServiceName}:{InstanceId}, Reason: {Reason}",
                instance.ServiceName,
                instance.InstanceId,
                reason
            );

            await _registry.UpdateHealthStatusAsync(instance.InstanceId, HealthStatus.Unhealthy, cancellationToken);

            // 清除缓存
            await _cache.ClearCacheAsync(instance.ServiceName, cancellationToken);
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _cts?.Dispose();
    }
}
