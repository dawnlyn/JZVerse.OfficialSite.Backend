using JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Builder;
using JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Configuration;
using JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Extensions;

/// <summary>
/// AppHost 扩展方法
/// </summary>
public static class AppHostExtensions
{
    /// <summary>
    /// 创建 AppHost 主机构建器
    /// </summary>
    public static IHostBuilder CreateAppHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices(
                (context, services) =>
                {
                    services.AddSingleton<ResourceLifecycleManager>();
                }
            );

    /// <summary>
    /// 添加 AppHost 服务
    /// </summary>
    public static IServiceCollection AddAppHost(this IServiceCollection services)
    {
        services.AddSingleton<ResourceLifecycleManager>();
        return services;
    }

    /// <summary>
    /// 从配置文件创建并运行分布式应用
    /// </summary>
    public static async Task RunFromConfigurationAsync(
        string configFilePath,
        CancellationToken cancellationToken = default
    )
    {
        var config = ConfigurationLoader.LoadFromFile(configFilePath);
        var basePath = Path.GetDirectoryName(Path.GetFullPath(configFilePath));
        var builder = config.ToBuilder(basePath);
        var app = builder.Build();

        await RunAsync(app, cancellationToken);
    }

    /// <summary>
    /// 运行分布式应用
    /// </summary>
    public static async Task RunAsync(DistributedApplication app, CancellationToken cancellationToken = default)
    {
        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(app);
                services.AddSingleton<ResourceLifecycleManager>();
                services.AddHostedService<AppHostBackgroundService>();
            })
            .Build();

        await host.RunAsync(cancellationToken);
    }
}

/// <summary>
/// AppHost 后台服务
/// </summary>
internal sealed class AppHostBackgroundService(
    DistributedApplication _app,
    ResourceLifecycleManager _lifecycleManager,
    ILogger<AppHostBackgroundService> _logger,
    IHostApplicationLifetime _lifetime
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting distributed application: {Name}", _app.Name);

        try
        {
            // 按依赖顺序启动资源
            var sortedResources = TopologicalSort(_app.Resources);

            foreach (var resource in sortedResources)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                await _lifecycleManager.StartResourceAsync(resource, stoppingToken);

                // 给资源一点时间启动
                await Task.Delay(500, stoppingToken);
            }

            _logger.LogInformation("All resources started successfully");

            // 等待取消信号
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Application shutdown requested");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running distributed application");
            _lifetime.StopApplication();
        }
        finally
        {
            // 按反向顺序停止资源
            var sortedResources = TopologicalSort(_app.Resources);
            sortedResources.Reverse();
            foreach (var resource in sortedResources)
            {
                try
                {
                    await _lifecycleManager.StopResourceAsync(resource);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error stopping resource: {Name}", resource.Name);
                }
            }
        }
    }

    private static List<Models.Resource> TopologicalSort(IReadOnlyList<Models.Resource> resources)
    {
        var sorted = new List<Models.Resource>();
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();

        foreach (var resource in resources)
        {
            Visit(resource);
        }

        return sorted;

        void Visit(Models.Resource resource)
        {
            if (visited.Contains(resource.Name))
                return;

            if (visiting.Contains(resource.Name))
                throw new InvalidOperationException($"Circular dependency detected at: {resource.Name}");

            visiting.Add(resource.Name);

            foreach (var dep in resource.Dependencies)
            {
                Visit(dep);
            }

            visiting.Remove(resource.Name);
            visited.Add(resource.Name);
            sorted.Add(resource);
        }
    }
}
