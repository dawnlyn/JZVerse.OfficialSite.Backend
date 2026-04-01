using JZVerse.MicroHuaxia.DataAccess.Abstractions;
using JZVerse.MicroHuaxia.DataAccess.Abstractions.Models;
using JZVerse.MicroHuaxia.DataAccess.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace JZVerse.MicroHuaxia.DataAccess.AspNetCore;

/// <summary>
/// ASP.NET Core 数据访问扩展方法
/// </summary>
public static class DataAccessExtensions
{
    /// <summary>
    /// 添加数据访问健康检查
    /// </summary>
    /// <param name="builder">健康检查构建器</param>
    /// <param name="name">健康检查名称</param>
    /// <param name="tags">标签</param>
    /// <returns>健康检查构建器</returns>
    public static IHealthChecksBuilder AddDataAccessHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "database",
        params string[] tags)
    {
        return builder.AddCheck<DataAccessHealthCheck>(
            name,
            tags: tags.Length > 0 ? tags : ["database", "ready"]);
    }

    /// <summary>
    /// 添加数据访问服务并配置健康检查
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureSnowflake">雪花 ID 配置委托</param>
    /// <param name="commandTimeout">命令超时时间（秒）</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddDataAccess(
        this IServiceCollection services,
        Action<SnowflakeIdGeneratorOptions>? configureSnowflake = null,
        int commandTimeout = 30)
    {
        services.AddDataAccessCore(configureSnowflake, commandTimeout);
        return services;
    }
}
