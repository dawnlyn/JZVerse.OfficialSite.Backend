using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.Configuration;
using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.Resilience;
using JZVerse.MicroHuaxia.ServiceCommunication.Core.DependencyInjection;
using JZVerse.MicroHuaxia.ServiceCommunication.Grpc.DependencyInjection;
using JZVerse.MicroHuaxia.ServiceCommunication.Http.DependencyInjection;
using JZVerse.MicroHuaxia.ServiceCommunication.JsonRpc.DependencyInjection;
using JZVerse.MicroHuaxia.ServiceDiscovery.AspNetCore.Extensions;
using JZVerse.MicroHuaxia.ServiceDiscovery.Client.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JZVerse.MicroHuaxia.ServiceCommunication.AspNetCore;

/// <summary>
/// 服务通信构建器
/// </summary>
public sealed class ServiceCommunicationBuilder(IServiceCollection services)
{
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// 添加服务发现集成
    /// </summary>
    public ServiceCommunicationBuilder AddServiceDiscovery(Action<ServiceDiscoveryClientOptions>? configure = null)
    {
        Services.AddServiceDiscoveryClient(configure ?? (_ => { }));
        return this;
    }

    /// <summary>
    /// 添加弹性策略配置
    /// </summary>
    public ServiceCommunicationBuilder AddResilience(Action<RetryPolicyOptions>? retryConfig = null, Action<CircuitBreakerOptions>? cbConfig = null)
    {
        if (retryConfig is not null)
        {
            Services.Configure(retryConfig);
        }

        if (cbConfig is not null)
        {
            Services.Configure(cbConfig);
        }

        return this;
    }

    /// <summary>
    /// 添加 HTTP 通信支持
    /// </summary>
    public ServiceCommunicationBuilder AddHttp()
    {
        Services.AddHttpServiceCommunication();
        return this;
    }

    /// <summary>
    /// 添加 gRPC 通信支持
    /// </summary>
    public ServiceCommunicationBuilder AddGrpc()
    {
        Services.AddGrpcServiceCommunication();
        return this;
    }

    /// <summary>
    /// 添加 JSON-RPC 通信支持
    /// </summary>
    public ServiceCommunicationBuilder AddJsonRpc()
    {
        Services.AddJsonRpcServiceCommunication();
        return this;
    }
}

/// <summary>
/// 服务通信扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加服务通信
    /// </summary>
    public static ServiceCommunicationBuilder AddServiceCommunication(
        this IServiceCollection services,
        Action<ServiceCommunicationOptions>? configure = null)
    {
        services.AddServiceCommunicationCore(configure);
        return new ServiceCommunicationBuilder(services);
    }
}
