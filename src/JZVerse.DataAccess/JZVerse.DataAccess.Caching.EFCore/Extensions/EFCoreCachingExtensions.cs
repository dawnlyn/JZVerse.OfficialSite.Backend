using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JZVerse.DataAccess.Caching.EFCore.Extensions;

/// <summary>
/// EF Core 缓存集成扩展方法
/// </summary>
public static class EFCoreCachingExtensions
{
    /// <summary>
    /// 添加 EF Core 缓存集成
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddEFCoreCaching(this IServiceCollection services)
    {
        services.TryAddSingleton<EFCoreCacheInterceptor>();
        return services;
    }

    /// <summary>
    /// 为 DbContext 添加缓存拦截器
    /// </summary>
    /// <param name="optionsBuilder">DbContext 选项构建器</param>
    /// <param name="serviceProvider">服务提供者</param>
    /// <returns>DbContext 选项构建器</returns>
    public static DbContextOptionsBuilder AddCachingInterceptor(
        this DbContextOptionsBuilder optionsBuilder,
        IServiceProvider serviceProvider)
    {
        var interceptor = serviceProvider.GetService<EFCoreCacheInterceptor>();

        if (interceptor is not null)
        {
            optionsBuilder.AddInterceptors(interceptor);
        }

        return optionsBuilder;
    }
}
