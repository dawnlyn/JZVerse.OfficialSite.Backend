using JZVerse.MicroHuaxia.DataAccess.Abstractions;
using JZVerse.MicroHuaxia.DataAccess.Abstractions.Models;
using JZVerse.MicroHuaxia.DataAccess.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JZVerse.MicroHuaxia.DataAccess.MongoDB;

/// <summary>
/// MongoDB 数据访问扩展方法
/// </summary>
public static class MongoDbDataAccessExtensions
{
    /// <summary>
    /// 添加 MongoDB 数据访问支持
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">连接配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddMongoDB(
        this IServiceCollection services,
        Action<MongoDbConnectionOptions> configure)
    {
        services.Configure(configure);
        services.TryAddSingleton<MongoDbConnectionFactory>();
        services.TryAddSingleton<IDbConnectionFactory>(sp => sp.GetRequiredService<MongoDbConnectionFactory>());
        return services;
    }

    /// <summary>
    /// 添加 MongoDB 数据访问支持（使用连接字符串）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="databaseName">数据库名称</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddMongoDB(
        this IServiceCollection services,
        string connectionString,
        string databaseName)
    {
        return services.AddMongoDB(options =>
        {
            options.ConnectionString = connectionString;
            options.DatabaseName = databaseName;
        });
    }

    /// <summary>
    /// 添加完整的 MongoDB 数据访问支持（包括雪花 ID）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="databaseName">数据库名称</param>
    /// <param name="configureSnowflake">雪花 ID 配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddMongoDBDataAccess(
        this IServiceCollection services,
        string connectionString,
        string databaseName,
        Action<SnowflakeIdGeneratorOptions>? configureSnowflake = null)
    {
        services.AddMongoDB(connectionString, databaseName);
        services.AddSnowflakeIdGenerator(configureSnowflake);
        // 注意：MongoDB 不使用 DapperExecutor，需要使用 MongoDB 驱动的原生 API
        return services;
    }
}
