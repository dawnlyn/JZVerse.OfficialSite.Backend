using JZVerse.MicroHuaxia.ServiceCommunication.JsonRpc.Client;
using JZVerse.MicroHuaxia.ServiceCommunication.JsonRpc.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JZVerse.MicroHuaxia.ServiceCommunication.JsonRpc.DependencyInjection;

/// <summary>
/// JSON-RPC 服务注册扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加 JSON-RPC 服务通信（客户端）
    /// </summary>
    public static IServiceCollection AddJsonRpcServiceCommunication(this IServiceCollection services)
    {
        services.TryAddSingleton<JsonRpcClient>();
        services.AddHttpClient("JsonRpc");

        return services;
    }

    /// <summary>
    /// 添加 JSON-RPC 服务端
    /// </summary>
    public static IServiceCollection AddJsonRpcServer(this IServiceCollection services)
    {
        services.TryAddSingleton<JsonRpcMethodRegistry>();

        return services;
    }

    /// <summary>
    /// 注册 JSON-RPC 处理器
    /// </summary>
    public static IServiceCollection AddJsonRpcHandler<THandler>(this IServiceCollection services)
        where THandler : class, IJsonRpcHandler
    {
        services.AddSingleton<IJsonRpcHandler, THandler>();

        return services;
    }
}

/// <summary>
/// 应用构建器扩展
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// 使用 JSON-RPC 中间件
    /// </summary>
    public static IApplicationBuilder UseJsonRpc(this IApplicationBuilder app)
    {
        // 注册所有处理器到注册表
        var registry = app.ApplicationServices.GetRequiredService<JsonRpcMethodRegistry>();
        var handlers = app.ApplicationServices.GetServices<IJsonRpcHandler>();

        foreach (var handler in handlers)
        {
            registry.Register(handler);
        }

        return app.UseMiddleware<JsonRpcMiddleware>();
    }
}
