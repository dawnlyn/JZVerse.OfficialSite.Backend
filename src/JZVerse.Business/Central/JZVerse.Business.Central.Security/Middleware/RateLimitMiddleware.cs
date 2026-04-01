using System.Net;
using JZVerse.Business.Central.Security.Services.Interfaces;

namespace JZVerse.Business.Central.Security.Middleware;

/// <summary>
/// 限流中间件
/// </summary>
public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRateLimitService _rateLimitService;
    private readonly ILogger<RateLimitMiddleware> _logger;
    private readonly RateLimitMiddlewareOptions _options;

    public RateLimitMiddleware(
        RequestDelegate next,
        IRateLimitService rateLimitService,
        ILogger<RateLimitMiddleware> logger,
        RateLimitMiddlewareOptions options)
    {
        _next = next;
        _rateLimitService = rateLimitService;
        _logger = logger;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 检查是否在白名单中
        if (_options.WhiteListPaths.Any(path => context.Request.Path.StartsWithSegments(path)))
        {
            await _next(context);
            return;
        }

        // 获取限流键
        var (keyType, keyValue) = GetRateLimitKey(context);

        // 检查限流
        var result = await _rateLimitService.CheckSlidingWindowAsync(
            keyType, 
            keyValue, 
            _options.DefaultLimit, 
            _options.WindowSeconds);

        // 添加限流响应头
        AddRateLimitHeaders(context.Response, result);

        if (!result.IsAllowed)
        {
            _logger.LogWarning("Rate limit exceeded for {KeyType}: {KeyValue}", keyType, keyValue);
            
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.ContentType = "application/json";
            
            var errorResponse = new
            {
                Code = 429,
                Message = "请求过于频繁，请稍后再试",
                RetryAfter = (int)(result.ResetTime - DateTime.UtcNow).TotalSeconds
            };
            
            await context.Response.WriteAsJsonAsync(errorResponse);
            return;
        }

        await _next(context);
    }

    private (string KeyType, string KeyValue) GetRateLimitKey(HttpContext context)
    {
        // 优先使用用户ID
        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId) && _options.EnableUserLimit)
        {
            return ("User", userId);
        }

        // 使用IP地址
        var ipAddress = GetClientIpAddress(context);
        return ("Ip", ipAddress);
    }

    private string GetClientIpAddress(HttpContext context)
    {
        // 优先从X-Forwarded-For获取（代理后）
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        // 从X-Real-IP获取
        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        // 使用连接IP
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static void AddRateLimitHeaders(HttpResponse response, RateLimitCheckResult result)
    {
        response.Headers["X-RateLimit-Limit"] = result.Limit.ToString();
        response.Headers["X-RateLimit-Remaining"] = result.Remaining.ToString();
        response.Headers["X-RateLimit-Reset"] = new DateTimeOffset(result.ResetTime).ToUnixTimeSeconds().ToString();
    }
}

/// <summary>
/// 限流中间件选项
/// </summary>
public class RateLimitMiddlewareOptions
{
    /// <summary>
    /// 默认限流次数
    /// </summary>
    public int DefaultLimit { get; set; } = 100;

    /// <summary>
    /// 窗口大小（秒）
    /// </summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// 是否启用用户限流
    /// </summary>
    public bool EnableUserLimit { get; set; } = true;

    /// <summary>
    /// 白名单路径
    /// </summary>
    public List<string> WhiteListPaths { get; set; } = new()
    {
        "/health",
        "/swagger"
    };
}
