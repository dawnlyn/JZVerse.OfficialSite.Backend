using JZVerse.DataAccess.Abstractions;
using JZVerse.DataAccess.Abstractions.Models;
using JZVerse.DataAccess.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JZVerse.DataAccess.PostgreSql;

/// <summary>
/// PostgreSQL 数据访问扩展方法
/// </summary>
public static class PostgreSqlDataAccessExtensions
{
    /// <summary>
    /// 添加 PostgreSQL 数据访问支持
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">连接配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddPostgreSql(
        this IServiceCollection services,
        Action<DbConnectionOptions> configure)
    {
        services.Configure<DbConnectionOptions>(options =>
        {
            options.DatabaseType = DatabaseType.PostgreSql;
            configure(options);
        });

        services.TryAddSingleton<IDbConnectionFactory, PostgreSqlConnectionFactory>();
        return services;
    }

    /// <summary>
    /// 添加 PostgreSQL 数据访问支持（使用连接字符串）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connectionString">连接字符串</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddPostgreSql(
        this IServiceCollection services,
        string connectionString)
    {
        return services.AddPostgreSql(options =>
        {
            options.ConnectionString = connectionString;
        });
    }

    /// <summary>
    /// 添加完整的 PostgreSQL 数据访问支持（包括雪花 ID 和执行器）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="configureSnowflake">雪花 ID 配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddPostgreSqlDataAccess(
        this IServiceCollection services,
        string connectionString,
        Action<SnowflakeIdGeneratorOptions>? configureSnowflake = null)
    {
        services.AddPostgreSql(connectionString);
        services.AddDataAccessCore(configureSnowflake);
        return services;
    }
}
