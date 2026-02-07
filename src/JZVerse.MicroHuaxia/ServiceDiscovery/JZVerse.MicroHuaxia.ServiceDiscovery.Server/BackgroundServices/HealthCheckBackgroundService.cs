using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Server.BackgroundServices;

/// <summary>
/// 健康检查后台服务
/// </summary>
public class HealthCheckBackgroundService(
    IHealthCheckManager _healthCheckManager,
    ILogger<HealthCheckBackgroundService> _logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Health check background service starting...");

        try
        {
            await _healthCheckManager.StartAsync(stoppingToken);

            // 等待取消信号
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Health check background service stopping...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in health check background service");
        }
        finally
        {
            await _healthCheckManager.StopAsync(CancellationToken.None);
            _logger.LogInformation("Health check background service stopped");
        }
    }
}
