using JZVerse.DataAccess.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JZVerse.DataAccess.Caching.Dapper.Extensions;

/// <summary>
/// Dapper 缓存集成扩展方法
/// </summary>
public static class DapperCachingExtensions
{
    /// <summary>
    /// 添加 Dapper 缓存集成
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    /// <remarks>
    /// 使用装饰器模式包装 IDbExecutor，自动为查询添加缓存支持
    /// </remarks>
    public static IServiceCollection AddDapperCaching(this IServiceCollection services)
    {
        // 装饰现有的 IDbExecutor
        services.Decorate<IDbExecutor, CachedDbExecutor>();

        return services;
    }
}

/// <summary>
/// 服务装饰器扩展
/// </summary>
internal static class ServiceDecoratorExtensions
{
    /// <summary>
    /// 使用装饰器模式包装服务
    /// </summary>
    public static IServiceCollection Decorate<TService, TDecorator>(this IServiceCollection services)
        where TService : class
        where TDecorator : class, TService
    {
        // 找到原有的服务注册
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(TService));

        if (descriptor is null)
        {
            throw new InvalidOperationException(
                $"Service {typeof(TService).Name} is not registered. " +
                $"Please register it before calling Decorate.");
        }

        // 移除原有注册
        services.Remove(descriptor);

        // 注册原有实现为内部服务
        var innerServiceKey = $"Inner_{typeof(TService).Name}";

        if (descriptor.ImplementationInstance is not null)
        {
            services.AddSingleton(descriptor.ImplementationInstance.GetType(), descriptor.ImplementationInstance);
        }
        else if (descriptor.ImplementationFactory is not null)
        {
            services.Add(new ServiceDescriptor(
                typeof(TService),
                sp => descriptor.ImplementationFactory(sp),
                descriptor.Lifetime));

            // 重新添加装饰器
            services.Add(new ServiceDescriptor(
                typeof(TService),
                sp =>
                {
                    var inner = (TService)descriptor.ImplementationFactory(sp);
                    return ActivatorUtilities.CreateInstance<TDecorator>(sp, inner);
                },
                descriptor.Lifetime));

            return services;
        }
        else if (descriptor.ImplementationType is not null)
        {
            services.Add(new ServiceDescriptor(
                descriptor.ImplementationType,
                descriptor.ImplementationType,
                descriptor.Lifetime));

            // 注册装饰器
            services.Add(new ServiceDescriptor(
                typeof(TService),
                sp =>
                {
                    var inner = (TService)sp.GetRequiredService(descriptor.ImplementationType);
                    return ActivatorUtilities.CreateInstance<TDecorator>(sp, inner);
                },
                descriptor.Lifetime));

            return services;
        }

        return services;
    }
}
