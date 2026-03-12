using JZVerse.MicroHuaxia.Saga.Client.Configuration;
using JZVerse.MicroHuaxia.Saga.Client.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JZVerse.MicroHuaxia.Saga.Client;

/// <summary>
/// Saga 客户端 DI 扩展方法
/// </summary>
public static class SagaClientExtensions
{
    /// <summary>
    /// 添加 Saga 客户端（从配置读取）
    /// </summary>
    public static IServiceCollection AddSagaClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SagaClientOptions>(
            configuration.GetSection(SagaClientOptions.SectionName));
        services.AddHttpClient<ISagaClient, HttpSagaClient>();
        return services;
    }

    /// <summary>
    /// 添加 Saga 客户端（编程式配置）
    /// </summary>
    public static IServiceCollection AddSagaClient(
        this IServiceCollection services,
        Action<SagaClientOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient<ISagaClient, HttpSagaClient>();
        return services;
    }
}
