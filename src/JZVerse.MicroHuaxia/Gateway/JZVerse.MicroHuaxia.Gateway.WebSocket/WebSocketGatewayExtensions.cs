using JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace JZVerse.MicroHuaxia.Gateway.WebSocket;

/// <summary>
/// WebSocket 网关扩展
/// </summary>
public static class WebSocketGatewayExtensions
{
    /// <summary>
    /// 添加 WebSocket 网关转发器
    /// </summary>
    public static IServiceCollection AddGatewayWebSocket(this IServiceCollection services)
    {
        services.AddSingleton<IProtocolForwarder, WebSocketForwarder>();
        return services;
    }

    /// <summary>
    /// 使用 WebSocket 网关
    /// </summary>
    public static IApplicationBuilder UseGatewayWebSocket(this IApplicationBuilder app)
    {
        app.UseWebSockets();
        return app;
    }
}
