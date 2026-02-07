using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace JZVerse.MicroHuaxia.Gateway.Tracing;

/// <summary>
/// 网关活动源
/// 用于创建和管理网关各环节的追踪 Activity
/// </summary>
public static class GatewayActivitySource
{
    /// <summary>
    /// 活动源名称
    /// </summary>
    public const string Name = "JZVerse.MicroHuaxia.Gateway";

    /// <summary>
    /// 活动源版本
    /// </summary>
    public const string Version = "1.0.0";

    /// <summary>
    /// 活动源实例
    /// </summary>
    public static ActivitySource Instance { get; } = new(Name, Version);

    // ==================== Activity 创建方法 ====================

    /// <summary>
    /// 开始网关请求追踪（顶层 Span）
    /// </summary>
    public static Activity? StartGatewayRequestActivity(HttpContext context)
    {
        var activity = Instance.StartActivity("gateway.request", ActivityKind.Server);

        if (activity != null)
        {
            activity.SetTag(GatewaySpanAttributes.RequestId, context.TraceIdentifier);
            activity.SetTag(GatewaySpanAttributes.HttpMethod, context.Request.Method);
            activity.SetTag(GatewaySpanAttributes.UrlPath, context.Request.Path.Value);
            activity.SetTag(GatewaySpanAttributes.ClientAddress, GetClientIp(context));

            if (context.Request.QueryString.HasValue)
            {
                activity.SetTag(GatewaySpanAttributes.UrlQuery, context.Request.QueryString.Value);
            }

            if (context.Request.ContentLength > 0)
            {
                activity.SetTag(GatewaySpanAttributes.HttpRequestBodySize, context.Request.ContentLength);
            }
        }

        return activity;
    }

    /// <summary>
    /// 开始路由匹配追踪
    /// </summary>
    public static Activity? StartRouteMatchingActivity(string path, string method)
    {
        var activity = Instance.StartActivity("gateway.routing", ActivityKind.Internal);

        activity?.SetTag(GatewaySpanAttributes.Operation, "routing");
        activity?.SetTag(GatewaySpanAttributes.UrlPath, path);
        activity?.SetTag(GatewaySpanAttributes.RoutingMethod, method);

        return activity;
    }

    /// <summary>
    /// 开始认证追踪
    /// </summary>
    public static Activity? StartAuthenticationActivity(string? strategy = null)
    {
        var activity = Instance.StartActivity("gateway.authentication", ActivityKind.Internal);

        activity?.SetTag(GatewaySpanAttributes.Operation, "authentication");
        if (strategy != null)
        {
            activity?.SetTag(GatewaySpanAttributes.AuthStrategy, strategy);
        }

        return activity;
    }

    /// <summary>
    /// 开始限流检查追踪
    /// </summary>
    public static Activity? StartRateLimitActivity(string key, string? algorithm = null)
    {
        var activity = Instance.StartActivity("gateway.ratelimit", ActivityKind.Internal);

        activity?.SetTag(GatewaySpanAttributes.Operation, "ratelimit");
        activity?.SetTag(GatewaySpanAttributes.RateLimitKey, key);
        if (algorithm != null)
        {
            activity?.SetTag(GatewaySpanAttributes.RateLimitAlgorithm, algorithm);
        }

        return activity;
    }

    /// <summary>
    /// 开始缓存操作追踪
    /// </summary>
    public static Activity? StartCacheActivity(string key, bool isRead)
    {
        var operationType = isRead ? "cache.read" : "cache.write";
        var activity = Instance.StartActivity($"gateway.{operationType}", ActivityKind.Internal);

        activity?.SetTag(GatewaySpanAttributes.Operation, operationType);
        activity?.SetTag(GatewaySpanAttributes.CacheKey, key);
        activity?.SetTag(GatewaySpanAttributes.CacheOperation, isRead ? "read" : "write");

        return activity;
    }

    /// <summary>
    /// 开始请求转发追踪
    /// </summary>
    public static Activity? StartForwardingActivity(string target, string? serviceName = null)
    {
        var activity = Instance.StartActivity("gateway.forwarding", ActivityKind.Client);

        activity?.SetTag(GatewaySpanAttributes.Operation, "forwarding");
        activity?.SetTag(GatewaySpanAttributes.ForwardingTarget, target);
        if (serviceName != null)
        {
            activity?.SetTag(GatewaySpanAttributes.ForwardingServiceName, serviceName);
        }

        return activity;
    }

    /// <summary>
    /// 开始重试追踪
    /// </summary>
    public static Activity? StartRetryActivity(int attempt, string? reason = null)
    {
        var activity = Instance.StartActivity("gateway.retry", ActivityKind.Internal);

        activity?.SetTag(GatewaySpanAttributes.Operation, "retry");
        activity?.SetTag(GatewaySpanAttributes.RetryAttempt, attempt);
        if (reason != null)
        {
            activity?.SetTag(GatewaySpanAttributes.RetryReason, reason);
        }

        return activity;
    }

    /// <summary>
    /// 开始舱壁执行追踪
    /// </summary>
    public static Activity? StartBulkheadActivity(string name, int currentConcurrency, int queueLength)
    {
        var activity = Instance.StartActivity("gateway.bulkhead", ActivityKind.Internal);

        activity?.SetTag(GatewaySpanAttributes.Operation, "bulkhead");
        activity?.SetTag(GatewaySpanAttributes.BulkheadName, name);
        activity?.SetTag(GatewaySpanAttributes.BulkheadConcurrency, currentConcurrency);
        activity?.SetTag(GatewaySpanAttributes.BulkheadQueueLength, queueLength);

        return activity;
    }

    /// <summary>
    /// 开始降级执行追踪
    /// </summary>
    public static Activity? StartFallbackActivity(string type, string? reason = null)
    {
        var activity = Instance.StartActivity("gateway.fallback", ActivityKind.Internal);

        activity?.SetTag(GatewaySpanAttributes.Operation, "fallback");
        activity?.SetTag(GatewaySpanAttributes.FallbackType, type);
        if (reason != null)
        {
            activity?.SetTag(GatewaySpanAttributes.FallbackReason, reason);
        }

        return activity;
    }

    // ==================== Activity 扩展方法 ====================

    /// <summary>
    /// 记录路由匹配结果
    /// </summary>
    public static void RecordRouteMatch(
        this Activity? activity,
        bool matched,
        string? routeId = null,
        string? routeName = null,
        string? pattern = null
    )
    {
        if (activity == null)
            return;

        activity.SetTag(GatewaySpanAttributes.RoutingMatched, matched);

        if (matched)
        {
            if (routeId != null)
                activity.SetTag(GatewaySpanAttributes.RouteId, routeId);
            if (routeName != null)
                activity.SetTag(GatewaySpanAttributes.RouteName, routeName);
            if (pattern != null)
                activity.SetTag(GatewaySpanAttributes.RoutingPattern, pattern);
        }
    }

    /// <summary>
    /// 记录认证结果
    /// </summary>
    public static void RecordAuthenticationResult(
        this Activity? activity,
        bool success,
        string? userId = null,
        string? strategy = null
    )
    {
        if (activity == null)
            return;

        activity.SetTag(GatewaySpanAttributes.AuthResult, success ? "success" : "failure");

        if (success && userId != null)
        {
            activity.SetTag(GatewaySpanAttributes.AuthUserId, userId);
        }

        if (strategy != null)
        {
            activity.SetTag(GatewaySpanAttributes.AuthStrategy, strategy);
        }

        if (!success)
        {
            activity.SetStatus(ActivityStatusCode.Error, "Authentication failed");
        }
    }

    /// <summary>
    /// 记录限流结果
    /// </summary>
    public static void RecordRateLimitResult(
        this Activity? activity,
        bool rejected,
        long? limit = null,
        long? remaining = null
    )
    {
        if (activity == null)
            return;

        activity.SetTag(GatewaySpanAttributes.RateLimitRejected, rejected);

        if (limit.HasValue)
            activity.SetTag(GatewaySpanAttributes.RateLimitLimit, limit.Value);
        if (remaining.HasValue)
            activity.SetTag(GatewaySpanAttributes.RateLimitRemaining, remaining.Value);

        if (rejected)
        {
            activity.SetStatus(ActivityStatusCode.Error, "Rate limited");
        }
    }

    /// <summary>
    /// 记录缓存结果
    /// </summary>
    public static void RecordCacheResult(this Activity? activity, bool hit, int? ttlSeconds = null)
    {
        if (activity == null)
            return;

        activity.SetTag(GatewaySpanAttributes.CacheHit, hit);

        if (ttlSeconds.HasValue)
        {
            activity.SetTag(GatewaySpanAttributes.CacheTtlSeconds, ttlSeconds.Value);
        }
    }

    /// <summary>
    /// 记录转发结果
    /// </summary>
    public static void RecordForwardingResult(
        this Activity? activity,
        int statusCode,
        long durationMs,
        long? responseBodySize = null
    )
    {
        if (activity == null)
            return;

        activity.SetTag(GatewaySpanAttributes.HttpStatusCode, statusCode);
        activity.SetTag(GatewaySpanAttributes.ForwardingDurationMs, durationMs);

        if (responseBodySize.HasValue)
        {
            activity.SetTag(GatewaySpanAttributes.HttpResponseBodySize, responseBodySize.Value);
        }

        if (statusCode >= 400)
        {
            activity.SetStatus(ActivityStatusCode.Error, $"HTTP {statusCode}");
        }
        else
        {
            activity.SetStatus(ActivityStatusCode.Ok);
        }
    }

    /// <summary>
    /// 记录异常
    /// </summary>
    public static void RecordException(this Activity? activity, Exception exception)
    {
        if (activity == null)
            return;

        activity.SetTag(GatewaySpanAttributes.ErrorType, exception.GetType().Name);
        activity.SetTag(GatewaySpanAttributes.ErrorMessage, exception.Message);
        activity.SetStatus(ActivityStatusCode.Error, exception.Message);

        activity.AddEvent(
            new ActivityEvent(
                "exception",
                tags: new ActivityTagsCollection
                {
                    { "exception.type", exception.GetType().FullName },
                    { "exception.message", exception.Message },
                    { "exception.stacktrace", exception.StackTrace },
                }
            )
        );
    }

    /// <summary>
    /// 设置成功状态
    /// </summary>
    public static void SetSuccess(this Activity? activity, string? message = null) =>
        activity?.SetStatus(ActivityStatusCode.Ok, message);

    /// <summary>
    /// 设置错误状态
    /// </summary>
    public static void SetError(this Activity? activity, string message) =>
        activity?.SetStatus(ActivityStatusCode.Error, message);

    /// <summary>
    /// 添加重试事件
    /// </summary>
    public static void AddRetryEvent(this Activity? activity, int attempt, TimeSpan delay, string? reason = null)
    {
        if (activity == null)
            return;

        var tags = new ActivityTagsCollection
        {
            { GatewaySpanAttributes.RetryAttempt, attempt },
            { GatewaySpanAttributes.RetryDelayMs, delay.TotalMilliseconds },
        };

        if (reason != null)
        {
            tags.Add(GatewaySpanAttributes.RetryReason, reason);
        }

        activity.AddEvent(new ActivityEvent("retry", tags: tags));
    }

    /// <summary>
    /// 添加熔断器状态变更事件
    /// </summary>
    public static void AddCircuitBreakerStateChangedEvent(
        this Activity? activity,
        string name,
        string newState,
        string? previousState = null
    )
    {
        if (activity == null)
            return;

        var tags = new ActivityTagsCollection
        {
            { GatewaySpanAttributes.CircuitBreakerName, name },
            { GatewaySpanAttributes.CircuitBreakerState, newState },
        };

        if (previousState != null)
        {
            tags.Add("gateway.circuitbreaker.previous_state", previousState);
        }

        activity.AddEvent(new ActivityEvent("circuit_breaker.state_changed", tags: tags));
    }

    /// <summary>
    /// 添加舱壁拒绝事件
    /// </summary>
    public static void AddBulkheadRejectedEvent(
        this Activity? activity,
        string name,
        int currentConcurrency,
        int queueLength
    )
    {
        if (activity == null)
            return;

        activity.AddEvent(
            new ActivityEvent(
                "bulkhead.rejected",
                tags: new ActivityTagsCollection
                {
                    { GatewaySpanAttributes.BulkheadName, name },
                    { GatewaySpanAttributes.BulkheadConcurrency, currentConcurrency },
                    { GatewaySpanAttributes.BulkheadQueueLength, queueLength },
                }
            )
        );
    }

    /// <summary>
    /// 添加降级执行事件
    /// </summary>
    public static void AddFallbackExecutedEvent(
        this Activity? activity,
        string type,
        string? source = null,
        string? reason = null
    )
    {
        if (activity == null)
            return;

        var tags = new ActivityTagsCollection { { GatewaySpanAttributes.FallbackType, type } };

        if (source != null)
        {
            tags.Add(GatewaySpanAttributes.FallbackSource, source);
        }

        if (reason != null)
        {
            tags.Add(GatewaySpanAttributes.FallbackReason, reason);
        }

        activity.AddEvent(new ActivityEvent("fallback.executed", tags: tags));
    }

    // ==================== 辅助方法 ====================

    private static string? GetClientIp(HttpContext context)
    {
        // 优先从 X-Forwarded-For 获取
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var ips = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (ips.Length > 0)
            {
                return ips[0].Trim();
            }
        }

        // 回退到连接 IP
        return context.Connection.RemoteIpAddress?.ToString();
    }
}
