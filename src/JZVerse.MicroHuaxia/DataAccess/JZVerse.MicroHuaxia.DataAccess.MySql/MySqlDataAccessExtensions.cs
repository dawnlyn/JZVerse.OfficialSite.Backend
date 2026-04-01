using JZVerse.MicroHuaxia.DataAccess.Abstractions;
using JZVerse.MicroHuaxia.DataAccess.Abstractions.Models;
using JZVerse.MicroHuaxia.DataAccess.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JZVerse.MicroHuaxia.DataAccess.MySql;

/// <summary>
/// MySQL 数据访问扩展方法
/// </summary>
public static class MySqlDataAccessExtensions
{
    /// <summary>
    /// 添加 MySQL 数据访问支持
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">连接配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddMySql(
        this IServiceCollection services,
        Action<DbConnectionOptions> configure)
    {
        services.Configure<DbConnectionOptions>(options =>
        {
            options.DatabaseType = DatabaseType.MySql;
            configure(options);
        });

        services.TryAddSingleton<IDbConnectionFactory, MySqlConnectionFactory>();
        return services;
    }

    /// <summary>
    /// 添加 MySQL 数据访问支持（使用连接字符串）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connectionString">连接字符串</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddMySql(
        this IServiceCollection services,
        string connectionString)
    {
        return services.AddMySql(options =>
        {
            options.ConnectionString = connectionString;
        });
    }

    /// <summary>
    /// 添加完整的 MySQL 数据访问支持（包括雪花 ID 和执行器）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="configureSnowflake">雪花 ID 配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddMySqlDataAccess(
        this IServiceCollection services,
        string connectionString,
        Action<SnowflakeIdGeneratorOptions>? configureSnowflake = null)
    {
        services.AddMySql(connectionString);
        services.AddDataAccessCore(configureSnowflake);
        return services;
    }
}
