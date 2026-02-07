using JZVerse.MicroHuaxia.ConfigCenter.Grpc.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace JZVerse.MicroHuaxia.ConfigCenter.Grpc.DependencyInjection;

/// <summary>
/// ConfigCenter.Grpc 服务注册扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加配置中心 gRPC 服务
    /// </summary>
    public static IServiceCollection AddConfigCenterGrpc(this IServiceCollection services)
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
    /// 映射配置中心 gRPC 服务
    /// </summary>
    public static void MapConfigCenterGrpc(this Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGrpcService<ConfigCenterGrpcService>();
    }
}
