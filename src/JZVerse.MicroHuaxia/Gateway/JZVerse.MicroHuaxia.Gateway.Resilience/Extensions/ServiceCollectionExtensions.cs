using JZVerse.MicroHuaxia.Gateway.Resilience.Abstractions;
using JZVerse.MicroHuaxia.Gateway.Resilience.Bulkhead;
using JZVerse.MicroHuaxia.Gateway.Resilience.Configuration;
using JZVerse.MicroHuaxia.Gateway.Resilience.Fallback;
using JZVerse.MicroHuaxia.Gateway.Resilience.Pipeline;
using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.Resilience;
using JZVerse.MicroHuaxia.ServiceCommunication.Core.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Gateway.Resilience.Extensions;

/// <summary>
/// Gateway 弹性能力 DI 扩展
/// </summary>
public static class GatewayResilienceServiceCollectionExtensions
{
    /// <summary>
    /// 添加 Gateway 弹性能力服务
    /// </summary>
    public static IServiceCollection AddGatewayResilience(
        this IServiceCollection services,
        Action<GatewayResilienceOptions>? configure = null)
    {
        // 配置选项
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<GatewayResilienceOptions>(_ => { });
        }

        // 注册 ServiceCommunication 基础弹性组件
        services.TryAddSingleton<ICircuitBreakerFactory>(sp =>
        {
            var options = sp.GetService<IOptions<CircuitBreakerOptions>>();
            var loggerFactory = sp.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>();
            return new CircuitBreakerFactory(options ?? Options.Create(new CircuitBreakerOptions()), loggerFactory);
        });

        // 注册舱壁工厂
        services.TryAddSingleton<IBulkheadFactory>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>();
            var options = sp.GetService<IOptions<GatewayResilienceOptions>>()?.Value;
            return new BulkheadFactory(loggerFactory, options?.DefaultBulkhead);
        });

        // 注册降级处理器
        services.TryAddSingleton<IFallbackHandler, StaticResponseFallbackHandler>();
        services.TryAddSingleton<IFallbackHandler, CachedResponseFallbackHandler>();

        // 注册降级处理器注册表
        services.TryAddSingleton<IFallbackHandlerRegistry, FallbackHandlerRegistry>();

        // 注册弹性管道工厂
        services.TryAddSingleton<IGatewayResiliencePipelineFactory, GatewayResiliencePipelineFactory>();

        return services;
    }

    /// <summary>
    /// 添加自定义降级处理器
    /// </summary>
    /// <typeparam name="T">处理器类型</typeparam>
    public static IServiceCollection AddCustomFallbackHandler<T>(this IServiceCollection services)
        where T : class, IFallbackHandler
    {
        services.AddSingleton<IFallbackHandler, T>();
        return services;
    }

    /// <summary>
    /// 添加自定义降级处理器（带工厂函数）
    /// </summary>
    public static IServiceCollection AddCustomFallbackHandler(
        this IServiceCollection services,
        Func<IServiceProvider, IFallbackHandler> factory)
    {
        services.AddSingleton(factory);
        return services;
    }
}
