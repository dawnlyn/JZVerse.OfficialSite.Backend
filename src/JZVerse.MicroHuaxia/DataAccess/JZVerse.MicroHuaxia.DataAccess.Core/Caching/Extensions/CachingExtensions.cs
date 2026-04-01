using JZVerse.MicroHuaxia.DataAccess.Abstractions.Caching;
using JZVerse.MicroHuaxia.DataAccess.Core.Caching.Providers;
using JZVerse.MicroHuaxia.DataAccess.Core.Caching.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JZVerse.MicroHuaxia.DataAccess.Core.Caching.Extensions;

/// <summary>
/// 缓存服务注册扩展方法
/// </summary>
public static class CachingExtensions
{
    /// <summary>
    /// 添加多级缓存核心服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddMultiLevelCaching(
        this IServiceCollection services,
        Action<CacheOptions>? configure = null)
    {
        // 配置选项
        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<CacheOptions>(_ => { });
        }

        // 添加内存缓存
        services.AddMemoryCache(options =>
        {
            // 默认配置，实际配置由 CacheOptions 控制
        });

        // 注册序列化器
        services.TryAddSingleton<ICacheSerializer, JsonCacheSerializer>();

        // 注册实体扫描器
        services.TryAddSingleton<ICacheableEntityScanner, CacheableEntityScanner>();

        // 注册缓存键生成器
        services.TryAddSingleton<ICacheKeyGenerator, CacheKeyGenerator>();

        // 注册缓存提供者
        services.TryAddSingleton<ICacheProvider, MemoryCacheProvider>();
        services.AddSingleton<ICacheProvider, RedisCacheProvider>();

        // 注册多级缓存协调器
        services.TryAddSingleton<IMultiLevelCache, MultiLevelCache>();

        // 注册预热服务
        services.TryAddSingleton<ICacheWarmer, CacheWarmer>();

        return services;
    }

    /// <summary>
    /// 添加多级缓存核心服务（从配置文件读取）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置节</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddMultiLevelCaching(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));

        return services.AddMultiLevelCaching(configure: null);
    }
}
