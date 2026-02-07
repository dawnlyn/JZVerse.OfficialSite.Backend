using JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace JZVerse.MicroHuaxia.Gateway.Http;

/// <summary>
/// HTTP 网关服务扩展
/// </summary>
public static class HttpGatewayExtensions
{
    /// <summary>
    /// 添加 HTTP 网关转发器
    /// </summary>
    public static IServiceCollection AddGatewayHttp(this IServiceCollection services)
    {
        services.AddHttpClient("GatewayForwarder")
            .ConfigureHttpClient(client =>
            {
                client.DefaultRequestHeaders.ConnectionClose = false;
            });

        services.AddSingleton<IRequestForwarder, HttpRequestForwarder>();

        return services;
    }
}
