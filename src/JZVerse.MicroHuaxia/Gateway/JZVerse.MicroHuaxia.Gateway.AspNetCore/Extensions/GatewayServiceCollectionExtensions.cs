using JZVerse.MicroHuaxia.Gateway.Abstractions;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Authentication;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Caching;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Configuration;
using JZVerse.MicroHuaxia.Gateway.Abstractions.RateLimiting;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;
using JZVerse.MicroHuaxia.Gateway.Abstractions.TrafficControl;
using JZVerse.MicroHuaxia.Gateway.Core;
using JZVerse.MicroHuaxia.Gateway.Core.Authentication;
using JZVerse.MicroHuaxia.Gateway.Core.Caching;
using JZVerse.MicroHuaxia.Gateway.Core.RateLimiting;
using JZVerse.MicroHuaxia.Gateway.Core.RateLimiting.Storage;
using JZVerse.MicroHuaxia.Gateway.Core.Repositories;
using JZVerse.MicroHuaxia.Gateway.Core.Routing;
using JZVerse.MicroHuaxia.Gateway.Core.TrafficControl;
using JZVerse.MicroHuaxia.Security;
using JZVerse.MicroHuaxia.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Gateway.AspNetCore.Extensions;

/// <summary>
/// 网关服务集合扩展
/// </summary>
public static class GatewayServiceCollectionExtensions
{
    /// <summary>
    /// 添加网关服务
    /// </summary>
    public static GatewayBuilder AddGateway(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GatewayOptions>(configuration.GetSection(GatewayOptions.SectionName));
        return services.AddGatewayCore();
    }

    /// <summary>
    /// 添加网关服务
    /// </summary>
    public static GatewayBuilder AddGateway(this IServiceCollection services, Action<GatewayOptions>? configure = null)
    {
        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<GatewayOptions>(_ => { });
        }

        return services.AddGatewayCore();
    }

    private static GatewayBuilder AddGatewayCore(this IServiceCollection services)
    {
        // 注册 Security 核心服务（国密算法）
        services.AddSingleton<ISm2Provider, Sm2Provider>();
        services.AddSingleton<ISm3Provider, Sm3Provider>();
        services.AddSingleton<ISm4Provider, Sm4Provider>();

        // 路由
        services.AddSingleton<IRouteRepository, InMemoryRouteRepository>();
        services.AddSingleton<IRouteMatchingEngine, RouteMatchingEngine>();

        // 认证
        services.AddSingleton<IAuthenticationStrategyRepository, InMemoryAuthenticationStrategyRepository>();
        services.AddSingleton<IAuthenticationPipeline, AuthenticationPipeline>();
        services.AddSingleton<ISecretEncryptor>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<GatewayOptions>>().Value;
            if (!string.IsNullOrEmpty(options.EncryptionMasterKey))
            {
                return new AesSecretEncryptor(options.EncryptionMasterKey);
            }

            return new NoOpSecretEncryptor();
        });

        // 注册认证处理器
        services.AddSingleton<JwtAuthenticationHandler>();
        services.AddSingleton<ApiKeyAuthenticationHandler>();
        services.AddSingleton<BasicAuthenticationHandler>();

        // 限流
        services.AddSingleton<IRateLimiterStore, MemoryRateLimiterStore>();
        services.AddSingleton<IRateLimiterFactory, RateLimiterFactory>();
        services.AddSingleton<IRateLimitKeyGenerator, RateLimitKeyGenerator>();

        // 流量控制
        services.AddSingleton<ITrafficColoringService, TrafficColoringService>();
        services.AddHttpClient("TrafficMirror", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddSingleton<ITrafficMirrorService, TrafficMirrorService>();

        // 缓存
        services.AddSingleton<IGatewayCache, InMemoryGatewayCache>();
        services.AddSingleton<ICacheKeyGenerator, CacheKeyGenerator>();

        // 审计
        services.AddSingleton<IAuditLogStore, InMemoryAuditLogStore>();
        services.AddSingleton<IAuditLogger, AuditLogger>();

        return new GatewayBuilder(services);
    }
}

/// <summary>
/// 网关构建器
/// </summary>
public sealed class GatewayBuilder
{
    public IServiceCollection Services { get; }

    public GatewayBuilder(IServiceCollection services)
    {
        Services = services;
    }

    /// <summary>
    /// 配置认证处理器
    /// </summary>
    public GatewayBuilder ConfigureAuthentication(Action<IServiceProvider, IAuthenticationPipeline> configure)
    {
        Services.AddSingleton<IConfigureOptions<object>>(sp =>
        {
            var pipeline = sp.GetRequiredService<IAuthenticationPipeline>();
            configure(sp, pipeline);
            return new ConfigureOptions<object>(_ => { });
        });

        return this;
    }

    /// <summary>
    /// 添加初始路由
    /// </summary>
    public GatewayBuilder AddRoutes(IEnumerable<GatewayRoute> routes)
    {
        Services.AddSingleton<IConfigureOptions<object>>(sp =>
        {
            var engine = sp.GetRequiredService<IRouteMatchingEngine>();
            engine.UpdateRoutes(routes);
            return new ConfigureOptions<object>(_ => { });
        });

        return this;
    }

    /// <summary>
    /// 添加初始认证策略
    /// </summary>
    public GatewayBuilder AddAuthenticationStrategies(IEnumerable<AuthenticationStrategy> strategies)
    {
        Services.AddSingleton<IConfigureOptions<object>>(sp =>
        {
            var repository = sp.GetRequiredService<IAuthenticationStrategyRepository>();
            foreach (var strategy in strategies)
            {
                repository.AddAsync(strategy).GetAwaiter().GetResult();
            }

            return new ConfigureOptions<object>(_ => { });
        });

        return this;
    }
}
