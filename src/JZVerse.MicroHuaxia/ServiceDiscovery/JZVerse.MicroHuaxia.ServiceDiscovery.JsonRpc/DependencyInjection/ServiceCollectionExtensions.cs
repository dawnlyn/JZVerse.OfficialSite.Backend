using JZVerse.MicroHuaxia.ServiceCommunication.JsonRpc.DependencyInjection;
using JZVerse.MicroHuaxia.ServiceDiscovery.JsonRpc.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.JsonRpc.DependencyInjection;

/// <summary>
/// ServiceDiscovery.JsonRpc 服务注册扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加服务发现 JSON-RPC 处理器
    /// </summary>
    public static IServiceCollection AddServiceDiscoveryJsonRpc(this IServiceCollection services)
    {
        services.AddJsonRpcServer();

        // 注册所有处理器
        services.AddJsonRpcHandler<RegisterHandler>();
        services.AddJsonRpcHandler<DeregisterHandler>();
        services.AddJsonRpcHandler<HeartbeatHandler>();
        services.AddJsonRpcHandler<GetInstancesHandler>();
        services.AddJsonRpcHandler<GetInstanceHandler>();
        services.AddJsonRpcHandler<DiscoverHandler>();
        services.AddJsonRpcHandler<GetServiceNamesHandler>();
        services.AddJsonRpcHandler<UpdateHealthStatusHandler>();

        return services;
    }
}
