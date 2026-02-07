using JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace JZVerse.MicroHuaxia.Gateway.Grpc;

/// <summary>
/// gRPC 网关扩展
/// </summary>
public static class GrpcGatewayExtensions
{
    /// <summary>
    /// 添加 gRPC 网关转发器
    /// </summary>
    public static IServiceCollection AddGatewayGrpc(this IServiceCollection services)
    {
        services.AddGrpc();
        services.AddSingleton<IProtocolForwarder, GrpcForwarder>();
        return services;
    }
}
