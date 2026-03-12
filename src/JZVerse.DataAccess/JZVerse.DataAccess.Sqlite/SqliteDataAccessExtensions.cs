using JZVerse.DataAccess.Abstractions;
using JZVerse.DataAccess.Abstractions.Models;
using JZVerse.DataAccess.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JZVerse.DataAccess.Sqlite;

/// <summary>
/// SQLite 数据访问扩展方法
/// </summary>
public static class SqliteDataAccessExtensions
{
    /// <summary>
    /// 添加 SQLite 数据访问支持
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">连接配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddSqlite(
        this IServiceCollection services,
        Action<DbConnectionOptions> configure)
    {
        services.Configure<DbConnectionOptions>(options =>
        {
            options.DatabaseType = DatabaseType.Sqlite;
            configure(options);
        });

        services.TryAddSingleton<IDbConnectionFactory, SqliteConnectionFactory>();
        return services;
    }

    /// <summary>
    /// 添加 SQLite 数据访问支持（使用连接字符串）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connectionString">连接字符串</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddSqlite(
        this IServiceCollection services,
        string connectionString)
    {
        return services.AddSqlite(options =>
        {
            options.ConnectionString = connectionString;
        });
    }

    /// <summary>
    /// 添加 SQLite 内存数据库支持
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddSqliteInMemory(this IServiceCollection services)
    {
        return services.AddSqlite("Data Source=:memory:");
    }

    /// <summary>
    /// 添加完整的 SQLite 数据访问支持（包括雪花 ID 和执行器）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="configureSnowflake">雪花 ID 配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddSqliteDataAccess(
        this IServiceCollection services,
        string connectionString,
        Action<SnowflakeIdGeneratorOptions>? configureSnowflake = null)
    {
        services.AddSqlite(connectionString);
        services.AddDataAccessCore(configureSnowflake);
        return services;
    }
}
