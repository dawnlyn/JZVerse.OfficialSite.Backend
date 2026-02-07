using System.Diagnostics;
using JZVerse.MicroHuaxia.Gateway.Abstractions;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Authentication;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Caching;
using JZVerse.MicroHuaxia.Gateway.Abstractions.RateLimiting;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;
using JZVerse.MicroHuaxia.Gateway.Abstractions.TrafficControl;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Gateway.AspNetCore.Middleware;

/// <summary>
/// 网关路由中间件
/// </summary>
public sealed class GatewayRoutingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GatewayRoutingMiddleware> _logger;

    public GatewayRoutingMiddleware(RequestDelegate next, ILogger<GatewayRoutingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IRouteMatchingEngine routeEngine,
        IRequestForwarder forwarder)
    {
        var matchResult = await routeEngine.MatchAsync(context);

        if (matchResult is null)
        {
            // 未匹配到路由，继续下一个中间件（可能是管理 API）
            await _next(context);
            return;
        }

        // 存储匹配结果供后续中间件使用
        context.Items["GatewayRoute"] = matchResult;

        _logger.LogDebug("路由匹配: {RouteId} -> {Path}", matchResult.Route.RouteId, context.Request.Path);

        // 转发请求
        await forwarder.ForwardAsync(context, matchResult);
    }
}

/// <summary>
/// 网关认证中间件
/// </summary>
public sealed class GatewayAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GatewayAuthenticationMiddleware> _logger;

    public GatewayAuthenticationMiddleware(RequestDelegate next, ILogger<GatewayAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAuthenticationPipeline authPipeline)
    {
        // 检查是否有匹配的路由
        if (context.Items.TryGetValue("GatewayRoute", out var routeObj) && routeObj is RouteMatchResult matchResult)
        {
            var authConfig = matchResult.Route.Authentication;
            if (authConfig is not null && authConfig.Required)
            {
                var result = await authPipeline.AuthenticateAsync(context, authConfig);
                if (!result.IsAuthenticated)
                {
                    _logger.LogWarning("认证失败: {Reason}", result.FailureReason);
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(new { error = "Unauthorized", message = result.FailureReason });
                    return;
                }

                context.Items["AuthenticationResult"] = result;
            }
        }

        await _next(context);
    }
}

/// <summary>
/// 网关限流中间件
/// </summary>
public sealed class GatewayRateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GatewayRateLimitMiddleware> _logger;

    public GatewayRateLimitMiddleware(RequestDelegate next, ILogger<GatewayRateLimitMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IRateLimiterFactory rateLimiterFactory,
        IRateLimitKeyGenerator keyGenerator)
    {
        if (context.Items.TryGetValue("GatewayRoute", out var routeObj) && routeObj is RouteMatchResult matchResult)
        {
            var rateLimitConfig = matchResult.Route.RateLimit;
            if (rateLimitConfig is not null && rateLimitConfig.Enabled)
            {
                var key = keyGenerator.GenerateKey(
                    context,
                    matchResult.Route.RouteId,
                    rateLimitConfig.KeyStrategy,
                    rateLimitConfig.CustomKeyHeader);

                var policy = new RateLimitPolicy
                {
                    Algorithm = rateLimitConfig.Algorithm,
                    Limit = rateLimitConfig.Limit,
                    Window = rateLimitConfig.Window,
                    TokensPerSecond = (double)rateLimitConfig.Limit / rateLimitConfig.Window.TotalSeconds,
                    BucketCapacity = rateLimitConfig.Limit
                };

                // 根据算法获取对应的限流器
                var rateLimiter = rateLimiterFactory.GetOrCreate(rateLimitConfig.Algorithm);
                var result = await rateLimiter.TryAcquireAsync(key, policy);

                // 添加限流响应头
                context.Response.Headers["X-RateLimit-Limit"] = result.Limit.ToString();
                context.Response.Headers["X-RateLimit-Remaining"] = result.Remaining.ToString();
                if (result.ResetAt.HasValue)
                {
                    context.Response.Headers["X-RateLimit-Reset"] = result.ResetAt.Value.ToUnixTimeSeconds().ToString();
                }

                if (!result.IsAllowed)
                {
                    _logger.LogWarning("请求被限流: {Key}, 算法: {Algorithm}", key, rateLimitConfig.Algorithm);
                    context.Items["WasRateLimited"] = true;
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    if (result.RetryAfter.HasValue)
                    {
                        context.Response.Headers["Retry-After"] = ((int)result.RetryAfter.Value.TotalSeconds).ToString();
                    }

                    await context.Response.WriteAsJsonAsync(new { error = "TooManyRequests", message = "请求过于频繁，请稍后重试" });
                    return;
                }
            }
        }

        await _next(context);
    }
}

/// <summary>
/// 网关流量染色中间件
/// </summary>
public sealed class GatewayTrafficColoringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GatewayTrafficColoringMiddleware> _logger;

    public GatewayTrafficColoringMiddleware(RequestDelegate next, ILogger<GatewayTrafficColoringMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITrafficColoringService coloringService)
    {
        if (context.Items.TryGetValue("GatewayRoute", out var routeObj) && routeObj is RouteMatchResult matchResult)
        {
            var coloringConfig = matchResult.Route.TrafficColoring;
            if (coloringConfig is not null && coloringConfig.Enabled)
            {
                var result = await coloringService.ApplyColoringAsync(context, coloringConfig);

                if (result.HasTags)
                {
                    // 存储染色标签供后续使用（负载均衡、路由等）
                    context.Items["TrafficTags"] = result.Tags;
                    context.Items["TrafficColoringResult"] = result;

                    // 添加染色标记到请求头（供下游服务使用）
                    var tagsHeader = string.Join(",", result.Tags);
                    context.Request.Headers["X-Traffic-Tag"] = tagsHeader;

                    _logger.LogDebug(
                        "流量已染色: {Tags}, 规则: {MatchedRule}",
                        tagsHeader, result.MatchedRule);
                }
            }
        }

        await _next(context);
    }
}

/// <summary>
/// 网关缓存中间件
/// </summary>
public sealed class GatewayCacheMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GatewayCacheMiddleware> _logger;

    public GatewayCacheMiddleware(RequestDelegate next, ILogger<GatewayCacheMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IGatewayCache cache,
        ICacheKeyGenerator keyGenerator)
    {
        // 仅缓存 GET 请求
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            await _next(context);
            return;
        }

        if (context.Items.TryGetValue("GatewayRoute", out var routeObj) && routeObj is RouteMatchResult matchResult)
        {
            var cacheConfig = matchResult.Route.Cache;
            if (cacheConfig is not null && cacheConfig.Enabled)
            {
                var cacheKey = keyGenerator.GenerateKey(context, cacheConfig);

                // 尝试从缓存获取
                var cachedResponse = await cache.TryGetAsync(cacheKey);
                if (cachedResponse is not null)
                {
                    _logger.LogDebug("缓存命中: {Key}", cacheKey);
                    context.Items["WasCacheHit"] = true;

                    context.Response.StatusCode = cachedResponse.StatusCode;
                    foreach (var (key, values) in cachedResponse.Headers)
                    {
                        context.Response.Headers[key] = values;
                    }

                    context.Response.Headers["X-Cache"] = "HIT";
                    await context.Response.Body.WriteAsync(cachedResponse.Body);
                    return;
                }

                // 缓存未命中，存储缓存键供后续写入
                context.Items["CacheKey"] = cacheKey;
                context.Items["CacheConfig"] = cacheConfig;
                context.Response.Headers["X-Cache"] = "MISS";
            }
        }

        await _next(context);
    }
}

/// <summary>
/// 网关审计中间件
/// </summary>
public sealed class GatewayAuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GatewayAuditMiddleware> _logger;

    public GatewayAuditMiddleware(RequestDelegate next, ILogger<GatewayAuditMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAuditLogger auditLogger)
    {
        var stopwatch = Stopwatch.StartNew();
        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            // 获取路由信息
            context.Items.TryGetValue("GatewayRoute", out var routeObj);
            var matchResult = routeObj as RouteMatchResult;

            // 获取认证信息
            context.Items.TryGetValue("AuthenticationResult", out var authObj);
            var authResult = authObj as AuthenticationResult;

            var entry = new AuditLogEntry
            {
                TraceId = traceId,
                RequestPath = context.Request.Path,
                Method = context.Request.Method,
                ClientIp = GetClientIp(context),
                UserAgent = context.Request.Headers.UserAgent,
                UserId = authResult?.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                AuthenticationMethod = authResult?.AuthenticationScheme,
                RouteId = matchResult?.Route.RouteId,
                TargetService = matchResult?.Route.Destination.ServiceName,
                TargetInstance = context.Items.TryGetValue("TargetInstance", out var instance) ? instance?.ToString() : null,
                StatusCode = context.Response.StatusCode,
                DurationMs = stopwatch.ElapsedMilliseconds,
                RequestBodySize = context.Request.ContentLength,
                ResponseBodySize = context.Response.ContentLength,
                WasRateLimited = context.Items.ContainsKey("WasRateLimited"),
                WasCacheHit = context.Items.ContainsKey("WasCacheHit"),
                WasCircuitBroken = context.Items.TryGetValue("WasCircuitBroken", out var circuitBroken) && circuitBroken is true,
                RetryCount = context.Items.TryGetValue("RetryCount", out var retryCount) && retryCount is int count ? count : 0,
                WasBulkheadRejected = context.Items.TryGetValue("WasBulkheadRejected", out var bulkheadRejected) && bulkheadRejected is true,
                FallbackExecuted = context.Items.TryGetValue("FallbackExecuted", out var fallbackExecuted) && fallbackExecuted is true,
                FallbackType = context.Items.TryGetValue("FallbackType", out var fallbackType) ? fallbackType?.ToString() : null,
                ErrorMessage = context.Items.TryGetValue("ErrorMessage", out var errorMsg) ? errorMsg?.ToString() : null
            };

            await auditLogger.LogAsync(entry);
        }
    }

    private static string? GetClientIp(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }
}
