using System.Diagnostics;
using JZVerse.MicroHuaxia.Gateway.Metrics.Configuration;
using JZVerse.MicroHuaxia.Gateway.Metrics.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Gateway.Metrics.Middleware;

/// <summary>
/// 网关指标收集中间件
/// 从 HttpContext.Items 中读取各种状态信息并记录指标
/// </summary>
public sealed class GatewayMetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly GatewayMetricsOptions _options;
    private readonly GatewayMetrics _metrics;
    private readonly IMetricsStore? _store;

    /// <summary>
    /// 创建网关指标收集中间件
    /// </summary>
    public GatewayMetricsMiddleware(
        RequestDelegate next,
        IOptions<GatewayMetricsOptions> options,
        GatewayMetrics metrics,
        IMetricsStore? store = null
    )
    {
        _next = next;
        _options = options.Value;
        _metrics = metrics;
        _store = store;
    }

    /// <summary>
    /// 处理请求
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled || ShouldIgnorePath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            RecordMetrics(context, stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private void RecordMetrics(HttpContext context, double durationMs)
    {
        // 获取路由信息
        string? routeId = null;
        if (context.Items.TryGetValue("GatewayRoute", out var routeObj) && routeObj != null)
        {
            // 使用反射获取 RouteId，避免直接依赖 Gateway.Abstractions
            var routeIdProp = routeObj.GetType().GetProperty("Route")?.GetValue(routeObj);
            if (routeIdProp != null)
            {
                routeId = routeIdProp.GetType().GetProperty("RouteId")?.GetValue(routeIdProp)?.ToString();
            }
        }

        var method = context.Request.Method;
        var statusCode = context.Response.StatusCode;
        var requestSize = context.Request.ContentLength;
        var responseSize = context.Response.ContentLength;

        // 记录请求指标
        if (_options.CollectRequestMetrics)
        {
            _metrics.RecordRequest(routeId, method, statusCode, durationMs, requestSize, responseSize);

            // 同步到内存存储
            if (_store != null)
            {
                var tags = new Dictionary<string, object?>
                {
                    [GatewayMetricTags.RouteId] = routeId ?? "unknown",
                    [GatewayMetricTags.HttpMethod] = method,
                    [GatewayMetricTags.HttpStatusCode] = statusCode,
                    [GatewayMetricTags.StatusCodeGroup] = GetStatusCodeGroup(statusCode),
                    [GatewayMetricTags.IsSuccess] = statusCode < 400,
                };

                _store.Record(GatewayMetricNames.RequestsTotal, 1, tags, MetricType.Counter);
                _store.Record(GatewayMetricNames.RequestDuration, durationMs, tags, MetricType.Histogram);

                if (requestSize.HasValue)
                {
                    _store.Record(GatewayMetricNames.RequestSize, requestSize.Value, tags, MetricType.Histogram);
                }

                if (responseSize.HasValue)
                {
                    _store.Record(GatewayMetricNames.ResponseSize, responseSize.Value, tags, MetricType.Histogram);
                }

                if (statusCode >= 400)
                {
                    var errorTags = new Dictionary<string, object?>
                    {
                        [GatewayMetricTags.RouteId] = routeId ?? "unknown",
                        [GatewayMetricTags.HttpStatusCode] = statusCode,
                        [GatewayMetricTags.ErrorType] = statusCode >= 500 ? "server_error" : "client_error",
                    };
                    _store.Record(GatewayMetricNames.ErrorsTotal, 1, errorTags, MetricType.Counter);
                }
            }
        }

        // 记录认证指标
        if (_options.CollectAuthMetrics && context.Items.TryGetValue("AuthenticationResult", out var authObj))
        {
            RecordAuthenticationMetrics(routeId, authObj);
        }

        // 记录限流指标
        if (_options.CollectRateLimitMetrics)
        {
            var wasRateLimited = context.Items.ContainsKey("WasRateLimited");
            RecordRateLimitMetrics(routeId, !wasRateLimited);
        }

        // 记录缓存指标
        if (_options.CollectCacheMetrics)
        {
            var wasCacheHit = context.Items.ContainsKey("WasCacheHit");
            if (wasCacheHit || context.Items.ContainsKey("CacheKey"))
            {
                RecordCacheMetrics(routeId, wasCacheHit);
            }
        }

        // 记录弹性指标
        if (_options.CollectResilienceMetrics)
        {
            RecordResilienceMetrics(context, routeId);
        }
    }

    private void RecordAuthenticationMetrics(string? routeId, object? authObj)
    {
        if (authObj == null)
            return;

        var authType = authObj.GetType();
        var isAuthenticated = authType.GetProperty("IsAuthenticated")?.GetValue(authObj) as bool? ?? false;
        var scheme = authType.GetProperty("AuthenticationScheme")?.GetValue(authObj)?.ToString() ?? "unknown";

        _metrics.RecordAuthentication(routeId, scheme, isAuthenticated, 0);

        if (_store != null)
        {
            var tags = new Dictionary<string, object?>
            {
                [GatewayMetricTags.RouteId] = routeId ?? "unknown",
                [GatewayMetricTags.AuthStrategy] = scheme,
                [GatewayMetricTags.AuthResult] = isAuthenticated ? "success" : "failure",
            };

            _store.Record(GatewayMetricNames.AuthAttempts, 1, tags, MetricType.Counter);
            if (!isAuthenticated)
            {
                _store.Record(GatewayMetricNames.AuthFailures, 1, tags, MetricType.Counter);
            }
        }
    }

    private void RecordRateLimitMetrics(string? routeId, bool allowed)
    {
        _metrics.RecordRateLimit(routeId, "sliding_window", "route", allowed);

        if (_store != null)
        {
            var tags = new Dictionary<string, object?>
            {
                [GatewayMetricTags.RouteId] = routeId ?? "unknown",
                [GatewayMetricTags.RateLimitAlgorithm] = "sliding_window",
                [GatewayMetricTags.RateLimitKeyStrategy] = "route",
            };

            if (allowed)
            {
                _store.Record(GatewayMetricNames.RateLimitAllowed, 1, tags, MetricType.Counter);
            }
            else
            {
                _store.Record(GatewayMetricNames.RateLimitBlocked, 1, tags, MetricType.Counter);
            }
        }
    }

    private void RecordCacheMetrics(string? routeId, bool hit)
    {
        _metrics.RecordCacheAccess(routeId, hit);

        if (_store != null)
        {
            var tags = new Dictionary<string, object?>
            {
                [GatewayMetricTags.RouteId] = routeId ?? "unknown",
                [GatewayMetricTags.CacheResult] = hit ? "hit" : "miss",
            };

            if (hit)
            {
                _store.Record(GatewayMetricNames.CacheHits, 1, tags, MetricType.Counter);
            }
            else
            {
                _store.Record(GatewayMetricNames.CacheMisses, 1, tags, MetricType.Counter);
            }
        }
    }

    private void RecordResilienceMetrics(HttpContext context, string? routeId)
    {
        // 重试
        if (context.Items.TryGetValue("RetryCount", out var retryCountObj) && retryCountObj is int retryCount && retryCount > 0)
        {
            _metrics.RecordRetry(routeId, retryCount);

            if (_store != null)
            {
                var tags = new Dictionary<string, object?>
                {
                    [GatewayMetricTags.RouteId] = routeId ?? "unknown",
                    [GatewayMetricTags.RetryAttempt] = retryCount,
                };
                _store.Record(GatewayMetricNames.RetryAttempts, retryCount, tags, MetricType.Counter);
            }
        }

        // 熔断
        if (context.Items.TryGetValue("WasCircuitBroken", out var circuitBroken) && circuitBroken is true)
        {
            _metrics.RecordCircuitBreakerTrip(routeId ?? "unknown", "open");

            if (_store != null)
            {
                var tags = new Dictionary<string, object?>
                {
                    [GatewayMetricTags.CircuitBreakerName] = routeId ?? "unknown",
                    [GatewayMetricTags.CircuitBreakerState] = "open",
                };
                _store.Record(GatewayMetricNames.CircuitBreakerTrips, 1, tags, MetricType.Counter);
            }
        }

        // 舱壁拒绝
        if (context.Items.TryGetValue("WasBulkheadRejected", out var bulkheadRejected) && bulkheadRejected is true)
        {
            _metrics.RecordBulkheadRejection(routeId ?? "unknown");

            if (_store != null)
            {
                var tags = new Dictionary<string, object?> { [GatewayMetricTags.BulkheadName] = routeId ?? "unknown" };
                _store.Record(GatewayMetricNames.BulkheadRejected, 1, tags, MetricType.Counter);
            }
        }

        // 降级
        if (context.Items.TryGetValue("FallbackExecuted", out var fallbackExecuted) && fallbackExecuted is true)
        {
            var fallbackType = context.Items.TryGetValue("FallbackType", out var ft) ? ft?.ToString() : "unknown";
            _metrics.RecordFallback(routeId, fallbackType ?? "unknown");

            if (_store != null)
            {
                var tags = new Dictionary<string, object?>
                {
                    [GatewayMetricTags.RouteId] = routeId ?? "unknown",
                    [GatewayMetricTags.FallbackType] = fallbackType ?? "unknown",
                };
                _store.Record(GatewayMetricNames.FallbackExecutions, 1, tags, MetricType.Counter);
            }
        }
    }

    private bool ShouldIgnorePath(PathString path)
    {
        if (!path.HasValue)
            return false;

        var pathValue = path.Value;
        foreach (var ignorePath in _options.IgnorePaths)
        {
            if (pathValue.StartsWith(ignorePath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetStatusCodeGroup(int statusCode) =>
        statusCode switch
        {
            >= 100 and < 200 => "1xx",
            >= 200 and < 300 => "2xx",
            >= 300 and < 400 => "3xx",
            >= 400 and < 500 => "4xx",
            >= 500 => "5xx",
            _ => "unknown",
        };
}
