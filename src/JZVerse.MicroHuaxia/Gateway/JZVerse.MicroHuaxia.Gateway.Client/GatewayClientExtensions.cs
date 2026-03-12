using JZVerse.MicroHuaxia.Gateway.Client.Configuration;
using JZVerse.MicroHuaxia.Gateway.Client.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JZVerse.MicroHuaxia.Gateway.Client;

/// <summary>
/// 网关客户端 DI 扩展方法
/// </summary>
public static class GatewayClientExtensions
{
    /// <summary>
    /// 添加网关客户端（从配置读取）
    /// </summary>
    public static IServiceCollection AddGatewayClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<GatewayClientOptions>(
            configuration.GetSection(GatewayClientOptions.SectionName));
        services.AddHttpClient<IGatewayClient, HttpGatewayClient>();
        return services;
    }

    /// <summary>
    /// 添加网关客户端（编程式配置）
    /// </summary>
    public static IServiceCollection AddGatewayClient(
        this IServiceCollection services,
        Action<GatewayClientOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient<IGatewayClient, HttpGatewayClient>();
        return services;
    }
}
