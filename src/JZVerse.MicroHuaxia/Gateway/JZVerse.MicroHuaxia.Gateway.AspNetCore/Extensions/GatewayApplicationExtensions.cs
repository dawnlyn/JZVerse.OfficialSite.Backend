using JZVerse.MicroHuaxia.Gateway.Abstractions.Authentication;
using JZVerse.MicroHuaxia.Gateway.AspNetCore.Middleware;
using JZVerse.MicroHuaxia.Gateway.Core.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace JZVerse.MicroHuaxia.Gateway.AspNetCore.Extensions;

/// <summary>
/// 网关应用程序扩展
/// </summary>
public static class GatewayApplicationExtensions
{
    /// <summary>
    /// 使用网关中间件
    /// </summary>
    public static IApplicationBuilder UseGateway(this IApplicationBuilder app)
    {
        // 初始化认证处理器
        InitializeAuthenticationHandlers(app);

        // 中间件顺序很重要
        // 1. 审计日志（记录所有请求）
        app.UseMiddleware<GatewayAuditMiddleware>();

        // 2. 认证（验证身份）
        app.UseMiddleware<GatewayAuthenticationMiddleware>();

        // 3. 流量染色（标记请求用于灰度/A/B 测试）
        app.UseMiddleware<GatewayTrafficColoringMiddleware>();

        // 4. 限流（防止过载）
        app.UseMiddleware<GatewayRateLimitMiddleware>();

        // 5. 缓存（读取缓存）
        app.UseMiddleware<GatewayCacheMiddleware>();

        // 6. 路由转发（核心功能）
        app.UseMiddleware<GatewayRoutingMiddleware>();

        return app;
    }

    /// <summary>
    /// 仅使用网关路由中间件
    /// </summary>
    public static IApplicationBuilder UseGatewayRouting(this IApplicationBuilder app)
    {
        app.UseMiddleware<GatewayRoutingMiddleware>();
        return app;
    }

    /// <summary>
    /// 使用网关认证中间件
    /// </summary>
    public static IApplicationBuilder UseGatewayAuthentication(this IApplicationBuilder app)
    {
        app.UseMiddleware<GatewayAuthenticationMiddleware>();
        return app;
    }

    /// <summary>
    /// 使用网关限流中间件
    /// </summary>
    public static IApplicationBuilder UseGatewayRateLimit(this IApplicationBuilder app)
    {
        app.UseMiddleware<GatewayRateLimitMiddleware>();
        return app;
    }

    /// <summary>
    /// 使用网关流量染色中间件
    /// </summary>
    public static IApplicationBuilder UseGatewayTrafficColoring(this IApplicationBuilder app)
    {
        app.UseMiddleware<GatewayTrafficColoringMiddleware>();
        return app;
    }

    /// <summary>
    /// 使用网关缓存中间件
    /// </summary>
    public static IApplicationBuilder UseGatewayCache(this IApplicationBuilder app)
    {
        app.UseMiddleware<GatewayCacheMiddleware>();
        return app;
    }

    /// <summary>
    /// 使用网关审计中间件
    /// </summary>
    public static IApplicationBuilder UseGatewayAudit(this IApplicationBuilder app)
    {
        app.UseMiddleware<GatewayAuditMiddleware>();
        return app;
    }

    /// <summary>
    /// 使用网关安全中间件（集成 Security 模块）
    /// </summary>
    public static IApplicationBuilder UseGatewaySecurity(this IApplicationBuilder app)
    {
        app.UseMiddleware<GatewaySecurityMiddleware>();
        return app;
    }

    private static void InitializeAuthenticationHandlers(IApplicationBuilder app)
    {
        var pipeline = app.ApplicationServices.GetRequiredService<IAuthenticationPipeline>();

        // 注册内置认证处理器
        var jwtHandler = app.ApplicationServices.GetRequiredService<JwtAuthenticationHandler>();
        var apiKeyHandler = app.ApplicationServices.GetRequiredService<ApiKeyAuthenticationHandler>();
        var basicHandler = app.ApplicationServices.GetRequiredService<BasicAuthenticationHandler>();

        pipeline.RegisterHandler(jwtHandler);
        pipeline.RegisterHandler(apiKeyHandler);
        pipeline.RegisterHandler(basicHandler);
    }
}
