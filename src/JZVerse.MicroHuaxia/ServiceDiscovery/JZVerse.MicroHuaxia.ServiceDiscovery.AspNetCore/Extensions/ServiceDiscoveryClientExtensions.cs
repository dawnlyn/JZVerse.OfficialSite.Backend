using JZVerse.MicroHuaxia.ServiceDiscovery.Client;
using JZVerse.MicroHuaxia.ServiceDiscovery.Client.BackgroundServices;
using JZVerse.MicroHuaxia.ServiceDiscovery.Client.Configuration;
using JZVerse.MicroHuaxia.ServiceDiscovery.Client.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.AspNetCore.Extensions;

/// <summary>
/// 服务发现客户端 ASP.NET Core 扩展方法
/// </summary>
public static class ServiceDiscoveryClientExtensions
{
    /// <summary>
    /// 添加服务发现客户端（从配置文件读取）
    /// </summary>
    public static IServiceCollection AddServiceDiscoveryClient(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        // 绑定配置
        services.Configure<ServiceDiscoveryClientOptions>(
            configuration.GetSection(ServiceDiscoveryClientOptions.SectionName)
        );

        return services.AddServiceDiscoveryClientCore();
    }

    /// <summary>
    /// 添加服务发现客户端（手动配置）
    /// </summary>
    public static IServiceCollection AddServiceDiscoveryClient(
        this IServiceCollection services,
        Action<ServiceDiscoveryClientOptions> configure
    )
    {
        services.Configure(configure);
        return services.AddServiceDiscoveryClientCore();
    }

    private static IServiceCollection AddServiceDiscoveryClientCore(this IServiceCollection services)
    {
        // 注册 HTTP 客户端
        services.AddHttpClient<IServiceDiscoveryClient, HttpServiceDiscoveryClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // 注册后台服务（自动注册、心跳）
        services.AddHostedService<ServiceRegistrationBackgroundService>();

        return services;
    }

    /// <summary>
    /// 添加服务发现客户端（仅发现，不注册）
    /// </summary>
    public static IServiceCollection AddServiceDiscoveryClientDiscoveryOnly(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        // 绑定配置
        services.Configure<ServiceDiscoveryClientOptions>(
            configuration.GetSection(ServiceDiscoveryClientOptions.SectionName)
        );

        // 注册 HTTP 客户端
        services.AddHttpClient<IServiceDiscoveryClient, HttpServiceDiscoveryClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // 不注册后台服务
        return services;
    }

    /// <summary>
    /// 添加服务发现客户端（仅发现，不注册，手动配置）
    /// </summary>
    public static IServiceCollection AddServiceDiscoveryClientDiscoveryOnly(
        this IServiceCollection services,
        Action<ServiceDiscoveryClientOptions> configure
    )
    {
        services.Configure(configure);

        // 注册 HTTP 客户端
        services.AddHttpClient<IServiceDiscoveryClient, HttpServiceDiscoveryClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // 不注册后台服务
        return services;
    }
}
