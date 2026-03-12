using JZVerse.MicroHuaxia.Saga.AspNetCore;
using JZVerse.MicroHuaxia.Saga.Server.Configuration;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Saga.Server.Extensions;

/// <summary>
/// Saga 服务端 DI 扩展方法
/// </summary>
public static class SagaServerExtensions
{
    /// <summary>
    /// 添加 Saga 服务端（从配置读取）
    /// </summary>
    public static IServiceCollection AddSagaServer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SagaServerOptions>(
            configuration.GetSection(SagaServerOptions.SectionName));

        return services.AddSagaServerCore();
    }

    /// <summary>
    /// 添加 Saga 服务端（编程式配置）
    /// </summary>
    public static IServiceCollection AddSagaServer(
        this IServiceCollection services,
        Action<SagaServerOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<SagaServerOptions>(_ => { });
        }

        return services.AddSagaServerCore();
    }

    private static IServiceCollection AddSagaServerCore(this IServiceCollection services)
    {
        // 使用 PostConfigure 来确保在所有 Configure 之后读取配置
        services.AddSaga(sagaOptions =>
        {
            // 需要从 DI 中获取已配置的 SagaServerOptions
            // 由于 AddSaga 是同步配置，使用 PostConfigure 模式
        });

        // 通过 PostConfigure 将 SagaServerOptions 映射到 SagaOptions
        services.AddSingleton<IConfigureOptions<SagaOptions>>(sp =>
        {
            var serverOptions = sp.GetRequiredService<IOptions<SagaServerOptions>>().Value;
            return new ConfigureNamedOptions<SagaOptions>(null, sagaOptions =>
            {
                sagaOptions.Storage = serverOptions.Storage;
                sagaOptions.CommunicationMode = serverOptions.CommunicationMode;
                sagaOptions.EnableTimeoutDetection = serverOptions.EnableTimeoutDetection;
                sagaOptions.EnableRecovery = serverOptions.EnableRecovery;
                sagaOptions.TimeoutOptions.DefaultTimeout = TimeSpan.FromSeconds(serverOptions.TimeoutSeconds);
                sagaOptions.RecoveryOptions.RecoveryInterval = TimeSpan.FromSeconds(serverOptions.RecoveryIntervalSeconds);
            });
        });

        return services;
    }
}
