using JZVerse.MicroHuaxia.ConfigCenter.Abstractions;
using JZVerse.MicroHuaxia.ConfigCenter.Core.Caching;
using JZVerse.MicroHuaxia.ConfigCenter.Core.Persistence;
using JZVerse.MicroHuaxia.ConfigCenter.Core.Repositories;
using JZVerse.MicroHuaxia.ConfigCenter.Core.Services;
using JZVerse.MicroHuaxia.ConfigCenter.Server.BackgroundServices;
using JZVerse.MicroHuaxia.ConfigCenter.Server.Configuration;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.ConfigCenter.Server.Extensions;

/// <summary>
/// 配置中心服务端扩展方法
/// </summary>
public static class ConfigCenterServerExtensions
{
    /// <summary>
    /// 添加配置中心服务端服务
    /// </summary>
    public static IServiceCollection AddConfigCenterServer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ConfigCenterServerOptions>(
            configuration.GetSection(ConfigCenterServerOptions.SectionName));

        return services.AddConfigCenterServerCore();
    }

    /// <summary>
    /// 添加配置中心服务端服务
    /// </summary>
    public static IServiceCollection AddConfigCenterServer(
        this IServiceCollection services,
        Action<ConfigCenterServerOptions>? configure = null)
    {
        if (configure is not null)
        {
            services.Configure(configure);
        }

        return services.AddConfigCenterServerCore();
    }

    private static IServiceCollection AddConfigCenterServerCore(this IServiceCollection services)
    {
        // 注册仓储（单例）
        services.AddSingleton<IConfigItemRepository, InMemoryConfigItemRepository>();
        services.AddSingleton<IConfigVersionRepository, InMemoryConfigVersionRepository>();
        services.AddSingleton<IGrayReleaseRepository, InMemoryGrayReleaseRepository>();

        // 注册事件发布器（单例）
        services.AddSingleton<IConfigEventPublisher, ConfigEventPublisher>();

        // 注册缓存（基于配置）
        services.AddSingleton<IConfigCache>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ConfigCenterServerOptions>>().Value;
            return options.EnableCache
                ? new InMemoryConfigCache(true, options.CacheTtlSeconds)
                : new NullConfigCache();
        });

        // 注册版本管理器（需要先于 ConfigRegistry 注册）
        services.AddSingleton<IConfigVersionManager, ConfigVersionManager>();

        // 注册核心服务（单例）
        services.AddSingleton<IConfigRegistry, ConfigRegistry>();
        services.AddSingleton<IConfigDiscovery, ConfigDiscoveryService>();
        services.AddSingleton<IGrayReleaseManager, GrayReleaseManager>();

        // 注册持久化存储
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ConfigCenterServerOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<FileConfigStore>>();
            return new FileConfigStore(options.PersistencePath, logger);
        });

        // 注册后台服务
        services.AddHostedService<ConfigSnapshotBackgroundService>();

        return services;
    }
}
