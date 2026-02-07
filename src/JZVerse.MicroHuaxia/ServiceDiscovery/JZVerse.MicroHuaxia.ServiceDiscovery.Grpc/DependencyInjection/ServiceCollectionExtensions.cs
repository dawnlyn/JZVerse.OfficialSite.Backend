using JZVerse.MicroHuaxia.ServiceDiscovery.Grpc.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Grpc.DependencyInjection;

/// <summary>
/// ServiceDiscovery.Grpc 服务注册扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加服务发现 gRPC 服务
    /// </summary>
    public static IServiceCollection AddServiceDiscoveryGrpc(this IServiceCollection services)
    {
        services.AddGrpc();
        return services;
    }
}

/// <summary>
/// 端点路由扩展
/// </summary>
public static class EndpointRouteBuilderExtensions
{
    /// <summary>
    /// 映射服务发现 gRPC 服务
    /// </summary>
    public static void MapServiceDiscoveryGrpc(this Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGrpcService<ServiceDiscoveryGrpcService>();
    }
}
