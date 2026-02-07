using JZVerse.MicroHuaxia.ServiceCommunication.Grpc.Interceptors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JZVerse.MicroHuaxia.ServiceCommunication.Grpc.DependencyInjection;

/// <summary>
/// gRPC 通信服务注册扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加 gRPC 服务通信
    /// </summary>
    public static IServiceCollection AddGrpcServiceCommunication(this IServiceCollection services)
    {
        // 注册 Channel 工厂
        services.TryAddSingleton<GrpcChannelFactory>();

        // 注册拦截器
        services.TryAddTransient<RetryInterceptor>();
        services.TryAddTransient<CircuitBreakerInterceptor>();
        services.TryAddTransient<LoggingInterceptor>();

        return services;
    }
}
