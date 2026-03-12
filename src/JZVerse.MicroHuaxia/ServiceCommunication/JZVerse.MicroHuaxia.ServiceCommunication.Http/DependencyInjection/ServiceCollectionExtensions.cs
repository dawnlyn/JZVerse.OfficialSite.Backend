using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions;
using JZVerse.MicroHuaxia.ServiceCommunication.Http.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JZVerse.MicroHuaxia.ServiceCommunication.Http.DependencyInjection;

/// <summary>
/// HTTP 通信服务注册扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加 HTTP 服务通信
    /// </summary>
    public static IServiceCollection AddHttpServiceCommunication(this IServiceCollection services)
    {
        // 注册 HTTP 服务客户端
        services.TryAddSingleton<IServiceClient, HttpServiceClient>();

        // 注册 HTTP 处理器
        services.TryAddTransient<ServiceDiscoveryHandler>();

        // 配置 HttpClient
        // DiagnosticsLoggingHandler 已迁移到 Observability.AspNetCore，通过 AddObservability() 注册
        services.AddHttpClient("ServiceCommunication")
            .AddHttpMessageHandler<ServiceDiscoveryHandler>()      // 解析服务名→真实地址
            .ConfigureHttpClient(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            });

        return services;
    }

    /// <summary>
    /// 添加类型化服务客户端
    /// </summary>
    /// <typeparam name="TService">服务接口类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="serviceName">服务名称</param>
    public static IServiceCollection AddTypedServiceClient<TService>(
        this IServiceCollection services,
        string serviceName) where TService : class
    {
        services.AddHttpClient(serviceName)
            .AddHttpMessageHandler<ServiceDiscoveryHandler>()
            .ConfigureHttpClient(client =>
            {
                client.DefaultRequestHeaders.Add("X-Service-Name", serviceName);
            });

        return services;
    }
}
