using JZVerse.MicroHuaxia.ConfigCenter.JsonRpc.Handlers;
using JZVerse.MicroHuaxia.ServiceCommunication.JsonRpc.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace JZVerse.MicroHuaxia.ConfigCenter.JsonRpc.DependencyInjection;

/// <summary>
/// ConfigCenter.JsonRpc 服务注册扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加配置中心 JSON-RPC 处理器
    /// </summary>
    public static IServiceCollection AddConfigCenterJsonRpc(this IServiceCollection services)
    {
        services.AddJsonRpcServer();

        // 注册所有处理器
        services.AddJsonRpcHandler<GetConfigHandler>();
        services.AddJsonRpcHandler<GetValueHandler>();
        services.AddJsonRpcHandler<GetNamespaceConfigHandler>();
        services.AddJsonRpcHandler<SetConfigHandler>();
        services.AddJsonRpcHandler<DeleteConfigHandler>();
        services.AddJsonRpcHandler<QueryConfigHandler>();

        return services;
    }
}
