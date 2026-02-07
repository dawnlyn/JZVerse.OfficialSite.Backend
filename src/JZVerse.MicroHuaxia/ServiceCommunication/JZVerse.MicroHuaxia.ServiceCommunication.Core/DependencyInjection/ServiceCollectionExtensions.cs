using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.Configuration;
using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.LoadBalancing;
using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.Resilience;
using JZVerse.MicroHuaxia.ServiceCommunication.Core.LoadBalancing;
using JZVerse.MicroHuaxia.ServiceCommunication.Core.Resilience;
using JZVerse.MicroHuaxia.ServiceCommunication.Core.ServiceDiscovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JZVerse.MicroHuaxia.ServiceCommunication.Core.DependencyInjection;

/// <summary>
/// ServiceCommunication.Core 服务注册扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加服务通信核心服务
    /// </summary>
    public static IServiceCollection AddServiceCommunicationCore(
        this IServiceCollection services,
        Action<ServiceCommunicationOptions>? configure = null)
    {
        // 配置选项
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.Configure<RetryPolicyOptions>(_ => { });
        services.Configure<CircuitBreakerOptions>(_ => { });

        // 注册负载均衡
        services.TryAddSingleton<IInstanceMetricsCollector, InstanceMetricsCollector>();
        services.TryAddSingleton<ILoadBalancerFactory>(sp =>
        {
            var metricsCollector = sp.GetService<IInstanceMetricsCollector>();
            return new LoadBalancerFactory(metricsCollector);
        });

        // 注册弹性策略
        services.TryAddSingleton<ICircuitBreakerFactory, CircuitBreakerFactory>();
        services.TryAddSingleton<IResiliencePipelineFactory, ResiliencePipelineFactory>();
        services.TryAddTransient<IRetryPolicy, ExponentialBackoffRetryPolicy>();

        // 注册服务实例选择器
        services.TryAddSingleton<IServiceInstanceSelector, ServiceInstanceSelector>();

        return services;
    }

    /// <summary>
    /// 配置重试策略
    /// </summary>
    public static IServiceCollection ConfigureRetryPolicy(
        this IServiceCollection services,
        Action<RetryPolicyOptions> configure)
    {
        services.Configure(configure);
        return services;
    }

    /// <summary>
    /// 配置熔断器
    /// </summary>
    public static IServiceCollection ConfigureCircuitBreaker(
        this IServiceCollection services,
        Action<CircuitBreakerOptions> configure)
    {
        services.Configure(configure);
        return services;
    }
}
