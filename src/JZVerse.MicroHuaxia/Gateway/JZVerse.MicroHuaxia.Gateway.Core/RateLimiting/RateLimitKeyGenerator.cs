using JZVerse.MicroHuaxia.Gateway.Abstractions.RateLimiting;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;
using Microsoft.AspNetCore.Http;

namespace JZVerse.MicroHuaxia.Gateway.Core.RateLimiting;

/// <summary>
/// 限流键生成器实现
/// </summary>
public sealed class RateLimitKeyGenerator : IRateLimitKeyGenerator
{
    public string GenerateKey(
        HttpContext context,
        string routeId,
        RateLimitKeyStrategy keyStrategy,
        string? customKeyHeader = null)
    {
        var baseKey = keyStrategy switch
        {
            RateLimitKeyStrategy.ClientIp => GetClientIp(context),
            RateLimitKeyStrategy.UserId => GetUserId(context),
            RateLimitKeyStrategy.Route => routeId,
            RateLimitKeyStrategy.Global => "global",
            RateLimitKeyStrategy.CustomHeader => GetCustomHeaderValue(context, customKeyHeader),
            _ => "unknown"
        };

        return $"ratelimit:{keyStrategy}:{baseKey}";
    }

    private static string GetClientIp(HttpContext context)
    {
        // 优先从 X-Forwarded-For 获取
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        // 从 X-Real-IP 获取
        var realIp = context.Request.Headers["X-Real-IP"].ToString();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        // 从连接获取
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static string GetUserId(HttpContext context)
    {
        // 从认证用户获取 ID
        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            return userId;
        }

        // 从 Name 获取
        var name = context.User.Identity?.Name;
        if (!string.IsNullOrEmpty(name))
        {
            return name;
        }

        // 回退到 IP
        return GetClientIp(context);
    }

    private static string GetCustomHeaderValue(HttpContext context, string? headerName)
    {
        if (string.IsNullOrEmpty(headerName))
        {
            return "unknown";
        }

        return context.Request.Headers.TryGetValue(headerName, out var value)
            ? value.ToString()
            : "unknown";
    }
}
