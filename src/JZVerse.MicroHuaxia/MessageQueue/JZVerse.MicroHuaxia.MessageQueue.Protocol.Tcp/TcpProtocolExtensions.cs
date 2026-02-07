using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JZVerse.MicroHuaxia.MessageQueue.Protocol.Tcp;

/// <summary>
/// TCP 协议服务扩展
/// </summary>
public static class TcpProtocolExtensions
{
    /// <summary>
    /// 添加 TCP 协议客户端
    /// </summary>
    public static IServiceCollection AddTcpProtocolClient(this IServiceCollection services)
    {
        services.TryAddTransient<TcpClientConnection>();
        services.TryAddSingleton<TcpFrameCodec>();
        return services;
    }

    /// <summary>
    /// 添加 TCP 协议服务端
    /// </summary>
    public static IServiceCollection AddTcpProtocolServer(this IServiceCollection services)
    {
        services.TryAddSingleton<TcpServerListener>();
        services.TryAddSingleton<TcpFrameCodec>();
        return services;
    }

    /// <summary>
    /// 添加 TCP 协议（客户端 + 服务端）
    /// </summary>
    public static IServiceCollection AddTcpProtocol(this IServiceCollection services)
    {
        services.AddTcpProtocolClient();
        services.AddTcpProtocolServer();
        return services;
    }
}
