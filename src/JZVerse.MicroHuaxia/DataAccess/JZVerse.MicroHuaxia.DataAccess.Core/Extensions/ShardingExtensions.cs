using JZVerse.MicroHuaxia.DataAccess.Abstractions;
using JZVerse.MicroHuaxia.DataAccess.Abstractions.Sharding;
using JZVerse.MicroHuaxia.DataAccess.Core.Executors;
using JZVerse.MicroHuaxia.DataAccess.Core.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JZVerse.MicroHuaxia.DataAccess.Core.Extensions;

/// <summary>
/// 分片数据访问扩展方法
/// </summary>
public static class ShardingExtensions
{
    /// <summary>
    /// 添加分片数据访问服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddShardingDataAccess(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 注册配置
        services.Configure<DataAccessOptions>(
            configuration.GetSection(DataAccessOptions.SectionName));

        // 注册核心服务
        services.AddSingleton<IEntityMetadataProvider, EntityMetadataProvider>();
        services.AddSingleton<IDatabaseRouter, DatabaseRouter>();
        services.AddSingleton<IMultiDbConnectionManager, MultiDbConnectionManager>();

        // 注册分片执行器
        services.AddScoped<IShardingDbExecutor, ShardingDbExecutor>();

        return services;
    }

    /// <summary>
    /// 添加分片数据访问服务（使用配置节）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <param name="sectionName">配置节名称</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddShardingDataAccess(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName)
    {
        services.Configure<DataAccessOptions>(
            configuration.GetSection(sectionName));

        services.AddSingleton<IEntityMetadataProvider, EntityMetadataProvider>();
        services.AddSingleton<IDatabaseRouter, DatabaseRouter>();
        services.AddSingleton<IMultiDbConnectionManager, MultiDbConnectionManager>();
        services.AddScoped<IShardingDbExecutor, ShardingDbExecutor>();

        return services;
    }

    /// <summary>
    /// 添加分片数据访问服务（使用选项配置）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">配置选项委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddShardingDataAccess(
        this IServiceCollection services,
        Action<DataAccessOptions> configureOptions)
    {
        services.Configure(configureOptions);

        services.AddSingleton<IEntityMetadataProvider, EntityMetadataProvider>();
        services.AddSingleton<IDatabaseRouter, DatabaseRouter>();
        services.AddSingleton<IMultiDbConnectionManager, MultiDbConnectionManager>();
        services.AddScoped<IShardingDbExecutor, ShardingDbExecutor>();

        return services;
    }

    /// <summary>
    /// 添加自定义分片策略
    /// </summary>
    /// <typeparam name="TStrategy">分片策略类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="name">策略名称</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddShardingStrategy<TStrategy>(
        this IServiceCollection services,
        string name) where TStrategy : class, IShardingStrategy
    {
        services.AddSingleton<IShardingStrategy, TStrategy>();
        return services;
    }
}
