using JZVerse.MicroHuaxia.Dashboard.Abstractions.Configuration;
using JZVerse.MicroHuaxia.Dashboard.Abstractions.Services;
using JZVerse.MicroHuaxia.Dashboard.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JZVerse.MicroHuaxia.Dashboard.AspNetCore.Extensions;

/// <summary>
/// Dashboard 服务注册扩展方法
/// </summary>
public static class DashboardServiceExtensions
{
    /// <summary>
    /// 添加 Dashboard 服务（从配置文件读取配置）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddDashboard(this IServiceCollection services, IConfiguration configuration)
    {
        // 绑定配置选项
        services.Configure<DashboardOptions>(configuration.GetSection(DashboardOptions.SectionName));

        return services.AddDashboardCore();
    }

    /// <summary>
    /// 添加 Dashboard 服务（手动配置）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddDashboard(
        this IServiceCollection services,
        Action<DashboardOptions>? configure = null
    )
    {
        if (configure != null)
        {
            services.Configure(configure);
        }

        return services.AddDashboardCore();
    }

    private static IServiceCollection AddDashboardCore(this IServiceCollection services)
    {
        // 注册数据服务
        services.AddScoped<IDashboardDataService, DashboardDataService>();

        // 注册 API 客户端（使用 HttpClientFactory）
        services.AddHttpClient<IServiceDiscoveryApiClient, ServiceDiscoveryApiClient>();
        services.AddHttpClient<IConfigCenterApiClient, ConfigCenterApiClient>();
        services.AddHttpClient<IGatewayApiClient, GatewayApiClient>();
        services.AddHttpClient<IMessageQueueApiClient, MessageQueueApiClient>();
        services.AddHttpClient<ISagaApiClient, SagaApiClient>();
        // 注册系统配置聚合服务
        services.AddScoped<ISystemConfigService, SystemConfigService>();

        // 注册配置迁移服务
        services.AddScoped<IConfigMigrationService, ConfigMigrationService>();

        // 添加 Ant Design Blazor
        services.AddAntDesign();

        return services;
    }
}
