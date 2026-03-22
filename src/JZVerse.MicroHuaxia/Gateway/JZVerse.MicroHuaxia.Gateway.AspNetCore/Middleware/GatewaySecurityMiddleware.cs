using JZVerse.MicroHuaxia.Security;
using Microsoft.AspNetCore.Http;

namespace JZVerse.MicroHuaxia.Gateway.AspNetCore.Middleware;

/// <summary>
/// 网关安全中间件
/// </summary>
/// <remarks>
/// 集成 Security 模块进行统一身份验证和授权决策
/// </remarks>
public sealed class GatewaySecurityMiddleware
{
    private readonly RequestDelegate _next;

    public GatewaySecurityMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 提取请求中的安全身份
        var identity = await ExtractIdentityAsync(context);
        
        if (identity != null)
        {
            // 将身份存入上下文，供后续中间件使用
            context.Items["SecurityIdentity"] = identity;
            
            // 添加请求头传递给下游服务
            context.Request.Headers.Append("X-Identity-Id", identity.IdentityId);
            context.Request.Headers.Append("X-Identity-Type", identity.Type.ToString());
            
            if (!string.IsNullOrEmpty(identity.Namespace))
            {
                context.Request.Headers.Append("X-Identity-Namespace", identity.Namespace);
            }
        }

        await _next(context);
    }

    /// <summary>
    /// 从请求中提取安全身份
    /// </summary>
    private Task<SecurityIdentity?> ExtractIdentityAsync(HttpContext context)
    {
        // 从 Authorization 头提取令牌
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader["Bearer ".Length..].Trim();
            
            // TODO: 调用 Security.Client 验证令牌
            // 现在先返回 null，等待 Security.Client 完整实现
            return Task.FromResult<SecurityIdentity?>(null);
        }

        // 从 X-Service-Identity 头提取服务身份（服务间调用）
        var serviceIdentity = context.Request.Headers["X-Service-Identity"].FirstOrDefault();
        if (!string.IsNullOrEmpty(serviceIdentity))
        {
            // TODO: 验证服务身份
            return Task.FromResult<SecurityIdentity?>(null);
        }

        return Task.FromResult<SecurityIdentity?>(null);
    }
}
