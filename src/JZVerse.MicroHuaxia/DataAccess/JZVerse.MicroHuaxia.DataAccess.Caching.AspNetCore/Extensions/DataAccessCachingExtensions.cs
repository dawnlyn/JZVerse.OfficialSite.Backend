using JZVerse.MicroHuaxia.DataAccess.Abstractions.Caching;
using JZVerse.MicroHuaxia.DataAccess.Caching.Dapper.Extensions;
using JZVerse.MicroHuaxia.DataAccess.Caching.EFCore.Extensions;
using JZVerse.MicroHuaxia.DataAccess.Core.Caching.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JZVerse.MicroHuaxia.DataAccess.Caching.AspNetCore.Extensions;

/// <summary>
/// 数据访问缓存扩展方法
/// </summary>
public static class DataAccessCachingExtensions
{
    /// <summary>
    /// 添加数据访问缓存支持
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddDataAccessCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 添加核心缓存服务
        services.AddMultiLevelCaching(configuration);

        return services;
    }

    /// <summary>
    /// 添加数据访问缓存支持
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddDataAccessCaching(
        this IServiceCollection services,
        Action<CacheOptions>? configure = null)
    {
        // 添加核心缓存服务
        services.AddMultiLevelCaching(configure);

        return services;
    }

    /// <summary>
    /// 添加 Dapper 缓存集成
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddDataAccessDapperCaching(this IServiceCollection services)
    {
        return services.AddDapperCaching();
    }

    /// <summary>
    /// 添加 EF Core 缓存集成
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddDataAccessEFCoreCaching(this IServiceCollection services)
    {
        return services.AddEFCoreCaching();
    }

    /// <summary>
    /// 添加缓存预热服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddCacheWarmup(this IServiceCollection services)
    {
        services.AddHostedService<CacheWarmupBackgroundService>();
        return services;
    }

    /// <summary>
    /// 添加缓存健康检查
    /// </summary>
    /// <param name="builder">健康检查构建器</param>
    /// <param name="name">检查名称</param>
    /// <param name="tags">标签</param>
    /// <returns>健康检查构建器</returns>
    public static IHealthChecksBuilder AddCachingHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "caching",
        params string[] tags)
    {
        return builder.AddCheck<CachingHealthCheck>(
            name,
            tags: tags.Length > 0 ? tags : ["caching", "ready"]);
    }
}
