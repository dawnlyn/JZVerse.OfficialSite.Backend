using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;
using JZVerse.MicroHuaxia.ServiceDiscovery.Core.Caching;
using JZVerse.MicroHuaxia.ServiceDiscovery.Core.LoadBalancing;
using JZVerse.MicroHuaxia.ServiceDiscovery.Core.Repositories;
using JZVerse.MicroHuaxia.ServiceDiscovery.Core.Services;
using JZVerse.MicroHuaxia.ServiceDiscovery.HealthChecks;
using JZVerse.MicroHuaxia.ServiceDiscovery.Server.BackgroundServices;
using JZVerse.MicroHuaxia.ServiceDiscovery.Server.Configuration;
using JZVerse.MicroHuaxia.ServiceDiscovery.Telemetry.Dashboard;
using JZVerse.MicroHuaxia.ServiceDiscovery.Telemetry.Metrics;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Server.Extensions;

/// <summary>
/// 服务发现服务端扩展方法
/// </summary>
public static class ServiceDiscoveryServerExtensions
{
    /// <summary>
    /// 添加服务发现服务端
    /// </summary>
    public static IServiceCollection AddServiceDiscoveryServer(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        // 绑定配置
        services.Configure<ServiceDiscoveryServerOptions>(
            configuration.GetSection(ServiceDiscoveryServerOptions.SectionName)
        );

        return services.AddServiceDiscoveryServerCore();
    }

    /// <summary>
    /// 添加服务发现服务端
    /// </summary>
    public static IServiceCollection AddServiceDiscoveryServer(
        this IServiceCollection services,
        Action<ServiceDiscoveryServerOptions>? configure = null
    )
    {
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<ServiceDiscoveryServerOptions>(_ => { });
        }

        return services.AddServiceDiscoveryServerCore();
    }

    private static IServiceCollection AddServiceDiscoveryServerCore(this IServiceCollection services)
    {
        // 注册核心服务（单例）
        services.AddSingleton<IServiceInstanceRepository, InMemoryServiceInstanceRepository>();
        services.AddSingleton<IServiceEventPublisher, ServiceEventPublisher>();

        // 注册缓存
        services.AddSingleton<IServiceDiscoveryCache>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ServiceDiscoveryServerOptions>>().Value;
            return new InMemoryServiceDiscoveryCache(options.EnableCache, options.CacheTtlSeconds);
        });

        // 注册负载均衡器
        services.AddSingleton<ILoadBalancer>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ServiceDiscoveryServerOptions>>().Value;
            return options.LoadBalancer.ToLowerInvariant() switch
            {
                "random" => new RandomLoadBalancer(),
                "weightedrandom" => new WeightedRandomLoadBalancer(),
                "weightedroundrobin" => new WeightedRoundRobinLoadBalancer(),
                _ => new RoundRobinLoadBalancer(),
            };
        });

        // 注册服务注册和发现
        services.AddSingleton<IServiceRegistry, ServiceRegistry>();
        services.AddSingleton<IServiceDiscovery, ServiceDiscoveryService>();

        // 注册健康检查
        services
            .AddHttpClient("HealthCheck")
            .ConfigureHttpClient(
                (sp, client) =>
                {
                    var options = sp.GetRequiredService<IOptions<ServiceDiscoveryServerOptions>>().Value;
                    client.Timeout = TimeSpan.FromSeconds(options.HealthCheckTimeoutSeconds);
                }
            );

        services.AddSingleton<IHealthChecker, HttpHealthChecker>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var logger = sp.GetRequiredService<ILogger<HttpHealthChecker>>();
            var options = sp.GetRequiredService<IOptions<ServiceDiscoveryServerOptions>>().Value;
            return new(httpClientFactory, logger, options.HealthCheckTimeoutSeconds);
        });

        services.AddSingleton<IHealthChecker, TcpHealthChecker>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<TcpHealthChecker>>();
            var options = sp.GetRequiredService<IOptions<ServiceDiscoveryServerOptions>>().Value;
            return new(logger, options.HealthCheckTimeoutSeconds);
        });

        // 注册健康检查管理器
        services.AddSingleton<IHealthCheckManager>(sp =>
        {
            var repository = sp.GetRequiredService<IServiceInstanceRepository>();
            var registry = sp.GetRequiredService<IServiceRegistry>();
            var cache = sp.GetRequiredService<IServiceDiscoveryCache>();
            var logger = sp.GetRequiredService<ILogger<HealthCheckManager>>();
            var options = sp.GetRequiredService<IOptions<ServiceDiscoveryServerOptions>>().Value;

            var manager = new HealthCheckManager(
                repository,
                registry,
                cache,
                logger,
                options.HealthCheckIntervalSeconds,
                options.HeartbeatTimeoutSeconds,
                options.FailureThreshold
            );

            // 注册所有健康检查器
            foreach (var checker in sp.GetServices<IHealthChecker>())
            {
                manager.RegisterChecker(checker);
            }

            return manager;
        });

        // 注册后台服务
        services.AddHostedService<HealthCheckBackgroundService>();

        // 配置 Telemetry 数据提供者 (如果可用)
        services.AddHostedService<TelemetryIntegrationService>();

        return services;
    }
}

/// <summary>
/// Telemetry 集成服务 - 配置指标和数据提供者的数据源
/// </summary>
internal sealed class TelemetryIntegrationService(IServiceProvider _serviceProvider) : IHostedService
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
