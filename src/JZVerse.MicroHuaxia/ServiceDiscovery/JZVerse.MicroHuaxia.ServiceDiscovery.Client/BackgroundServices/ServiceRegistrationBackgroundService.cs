using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;
using JZVerse.MicroHuaxia.ServiceDiscovery.Client.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Client.BackgroundServices;

/// <summary>
/// 服务注册后台服务
/// </summary>
public class ServiceRegistrationBackgroundService(
    IServiceDiscoveryClient _client,
    IOptions<ServiceDiscoveryClientOptions> options,
    ILogger<ServiceRegistrationBackgroundService> _logger
) : BackgroundService
{
    private readonly ServiceDiscoveryClientOptions _options = options.Value;

    private string? _instanceId;
    private PeriodicTimer? _heartbeatTimer;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 检查是否启用自动注册
        if (!_options.Service.AutoRegister || string.IsNullOrEmpty(_options.Service.ServiceName))
        {
            _logger.LogInformation("Auto registration is disabled or service name is not configured");
            return;
        }

        // 等待应用程序启动完成
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        try
        {
            // 注册服务
            await RegisterServiceAsync(stoppingToken);

            // 启动心跳
            if (_options.Heartbeat.Enabled && _instanceId != null)
            {
                await RunHeartbeatLoopAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // 应用程序正在关闭
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in service registration background service");
        }
        finally
        {
            // 注销服务
            if (_options.Service.AutoDeregister && _instanceId != null)
            {
                await DeregisterServiceAsync();
            }
        }
    }

    private async Task RegisterServiceAsync(CancellationToken cancellationToken)
    {
        var registration = new ServiceRegistration
        {
            ServiceName = _options.Service.ServiceName,
            Version = _options.Service.Version,
            Host = _options.Service.Host,
            Port = _options.Service.Port,
            Scheme = _options.Service.Scheme,
            BasePath = _options.Service.BasePath,
            Tags = _options.Service.Tags,
            Weight = _options.Service.Weight,
            HeartbeatIntervalSeconds = _options.Heartbeat.IntervalSeconds,
            Metadata = new()
            {
                Properties = _options.Service.Metadata,
                HealthCheckEndpoint = _options.HealthCheck.Endpoint,
            },
            HealthCheck = new()
            {
                EnableActiveCheck = _options.HealthCheck.Enabled,
                Endpoint = _options.HealthCheck.Endpoint,
            },
        };

        const int maxRetries = 5;
        var retryDelay = TimeSpan.FromSeconds(2);

        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                var instance = await _client.RegisterAsync(registration, cancellationToken);
                _instanceId = instance.InstanceId;

                _logger.LogInformation(
                    "Service registered successfully: {ServiceName}:{InstanceId}",
                    instance.ServiceName,
                    instance.InstanceId
                );

                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to register service (attempt {Attempt}/{MaxRetries})",
                    i + 1,
                    maxRetries
                );

                if (i < maxRetries - 1)
                {
                    await Task.Delay(retryDelay, cancellationToken);
                    retryDelay *= 2; // 指数退避
                }
            }
        }

        _logger.LogError("Failed to register service after {MaxRetries} attempts", maxRetries);
    }

    private async Task RunHeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        _heartbeatTimer = new(TimeSpan.FromSeconds(_options.Heartbeat.IntervalSeconds));

        while (await _heartbeatTimer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                var success = await _client.HeartbeatAsync(_instanceId!, cancellationToken);

                if (!success)
                {
                    _logger.LogWarning(
                        "Heartbeat failed for instance {InstanceId}, attempting to re-register",
                        _instanceId
                    );

                    // 尝试重新注册
                    await RegisterServiceAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error sending heartbeat for instance {InstanceId}", _instanceId);
            }
        }
    }

    private async Task DeregisterServiceAsync()
    {
        if (_instanceId == null)
            return;

        try
        {
            var success = await _client.DeregisterAsync(_instanceId, CancellationToken.None);

            if (success)
            {
                _logger.LogInformation("Service deregistered successfully: {InstanceId}", _instanceId);
            }
            else
            {
                _logger.LogWarning("Failed to deregister service: {InstanceId}", _instanceId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error deregistering service: {InstanceId}", _instanceId);
        }
    }

    public override void Dispose()
    {
        _heartbeatTimer?.Dispose();
        base.Dispose();
    }
}
