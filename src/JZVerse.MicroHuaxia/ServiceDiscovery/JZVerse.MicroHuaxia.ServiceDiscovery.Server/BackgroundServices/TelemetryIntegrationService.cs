using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;
using JZVerse.MicroHuaxia.ServiceDiscovery.Server.Telemetry.Dashboard;
using JZVerse.MicroHuaxia.ServiceDiscovery.Server.Telemetry.Metrics;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Server.BackgroundServices;

/// <summary>
/// Telemetry 集成服务 - 配置指标和数据提供者的数据源
/// </summary>
public sealed class TelemetryIntegrationService(IServiceProvider _serviceProvider) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // 配置指标收集器的数据源
        var metrics = _serviceProvider.GetService<ServiceDiscoveryMetrics>();
        var telemetryProvider = _serviceProvider.GetService<ITelemetryDataProvider>() as TelemetryDataProvider;
        var repository = _serviceProvider.GetRequiredService<IServiceInstanceRepository>();

        var getTotalServices = () =>
        {
            try
            {
                var instances = repository.GetAllAsync(CancellationToken.None).GetAwaiter().GetResult();
                return instances.Select(i => i.ServiceName).Distinct().Count();
            }
            catch
            {
                return 0;
            }
        };

        var getTotalInstances = () =>
        {
            try
            {
                return repository.GetAllAsync(CancellationToken.None).GetAwaiter().GetResult().Count;
            }
            catch
            {
                return 0;
            }
        };

        var getHealthyInstances = () =>
        {
            try
            {
                var instances = repository.GetAllAsync(CancellationToken.None).GetAwaiter().GetResult();
                return instances.Count(i => i.Health == HealthStatus.Healthy);
            }
            catch
            {
                return 0;
            }
        };

        var getUnhealthyInstances = () =>
        {
            try
            {
                var instances = repository.GetAllAsync(CancellationToken.None).GetAwaiter().GetResult();
                return instances.Count(i => i.Health == HealthStatus.Unhealthy);
            }
            catch
            {
                return 0;
            }
        };

        metrics?.SetInstanceStatsProvider(
            getTotalServices,
            getTotalInstances,
            getHealthyInstances,
            getUnhealthyInstances
        );

        telemetryProvider?.SetInstanceStatsSource(
            getTotalServices,
            getTotalInstances,
            getHealthyInstances,
            getUnhealthyInstances
        );

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
