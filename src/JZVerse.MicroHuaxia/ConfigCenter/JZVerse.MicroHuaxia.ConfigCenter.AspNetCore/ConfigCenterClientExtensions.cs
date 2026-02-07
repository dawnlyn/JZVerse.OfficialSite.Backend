using JZVerse.MicroHuaxia.ConfigCenter.Client;
using JZVerse.MicroHuaxia.ConfigCenter.Client.Configuration;
using JZVerse.MicroHuaxia.ConfigCenter.Client.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JZVerse.MicroHuaxia.ConfigCenter.AspNetCore;

/// <summary>
/// 配置中心客户端扩展方法
/// </summary>
public static class ConfigCenterClientExtensions
{
    /// <summary>
    /// 添加配置中心客户端
    /// </summary>
    public static IServiceCollection AddConfigCenter(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ConfigCenterClientOptions>(
            configuration.GetSection(ConfigCenterClientOptions.SectionName));

        return services.AddConfigCenterCore();
    }

    /// <summary>
    /// 添加配置中心客户端
    /// </summary>
    public static IServiceCollection AddConfigCenter(
        this IServiceCollection services,
        Action<ConfigCenterClientOptions> configure)
    {
        services.Configure(configure);
        return services.AddConfigCenterCore();
    }

    private static IServiceCollection AddConfigCenterCore(this IServiceCollection services)
    {
        // 注册 HttpClient
        services.AddHttpClient<IConfigCenterClient, HttpConfigCenterClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ConfigCenterClientOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(options.HttpTimeoutSeconds);
        });

        // 注册后台同步服务
        services.AddHostedService<ConfigSyncBackgroundService>();

        return services;
    }
}
