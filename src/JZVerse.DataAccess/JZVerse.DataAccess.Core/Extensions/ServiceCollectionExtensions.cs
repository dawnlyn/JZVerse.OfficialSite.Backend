using JZVerse.DataAccess.Abstractions;
using JZVerse.DataAccess.Abstractions.Models;
using JZVerse.DataAccess.Core.Executors;
using JZVerse.DataAccess.Core.IdGeneration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JZVerse.DataAccess.Core.Extensions;

/// <summary>
/// 数据访问层服务注册扩展方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加雪花 ID 生成器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddSnowflakeIdGenerator(
        this IServiceCollection services,
        Action<SnowflakeIdGeneratorOptions>? configure = null)
    {
        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<SnowflakeIdGeneratorOptions>(_ => { });
        }

        services.TryAddSingleton<ISnowflakeIdGenerator, SnowflakeIdGenerator>();
        return services;
    }

    /// <summary>
    /// 添加 Dapper 执行器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="commandTimeout">命令超时时间（秒）</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddDapperExecutor(
        this IServiceCollection services,
        int commandTimeout = 30)
    {
        services.TryAddScoped<IDbExecutor>(sp =>
        {
            var connectionFactory = sp.GetRequiredService<IDbConnectionFactory>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DapperExecutor>>();
            return new DapperExecutor(connectionFactory, logger, commandTimeout);
        });

        return services;
    }

    /// <summary>
    /// 添加数据访问核心服务（雪花 ID + Dapper 执行器）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureSnowflake">雪花 ID 配置委托</param>
    /// <param name="commandTimeout">命令超时时间（秒）</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddDataAccessCore(
        this IServiceCollection services,
        Action<SnowflakeIdGeneratorOptions>? configureSnowflake = null,
        int commandTimeout = 30)
    {
        services.AddSnowflakeIdGenerator(configureSnowflake);
        services.AddDapperExecutor(commandTimeout);
        return services;
    }
}
