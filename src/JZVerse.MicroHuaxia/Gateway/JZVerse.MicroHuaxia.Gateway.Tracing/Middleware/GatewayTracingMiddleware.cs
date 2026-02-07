using System.Diagnostics;
using JZVerse.MicroHuaxia.Gateway.Tracing.Configuration;
using JZVerse.MicroHuaxia.Gateway.Tracing.Propagation;
using JZVerse.MicroHuaxia.Gateway.Tracing.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Gateway.Tracing.Middleware;

/// <summary>
/// 网关追踪中间件
/// 作为管道最外层，创建顶层追踪 Span 并管理追踪上下文
/// </summary>
public sealed class GatewayTracingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly GatewayTracingOptions _options;
    private readonly ITracePropagator _propagator;
    private readonly ITraceStore? _traceStore;
    private readonly ILogger<GatewayTracingMiddleware> _logger;

    public GatewayTracingMiddleware(
        RequestDelegate next,
        IOptions<GatewayTracingOptions> options,
        ITracePropagator propagator,
        ILogger<GatewayTracingMiddleware> logger,
        ITraceStore? traceStore = null
    )
    {
        _next = next;
        _options = options.Value;
        _propagator = propagator;
        _traceStore = traceStore;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 检查是否启用追踪
        if (!_options.Enabled)
        {
            await _next(context);
            return;
        }

        // 检查是否忽略该路径
        if (ShouldIgnorePath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // 尝试从上游提取追踪上下文
        var parentContext = _propagator.Extract(context);
        Activity? activity = null;

        try
        {
            // 创建或继续追踪
            if (parentContext.IsValid)
            {
                // 继续上游追踪
                var activityContext = new ActivityContext(
                    parentContext.TraceId,
                    parentContext.SpanId,
                    parentContext.TraceFlags,
                    parentContext.TraceState,
                    isRemote: true
                );

                activity = GatewayActivitySource.Instance.StartActivity(
                    "gateway.request",
                    ActivityKind.Server,
                    activityContext
                );
            }
            else
            {
                // 创建新的追踪
                activity = GatewayActivitySource.StartGatewayRequestActivity(context);
            }

            if (activity != null)
            {
                // 存储到 HttpContext.Items 供其他中间件使用
                context.Items["GatewayActivity"] = activity;
                context.Items["GatewayTraceId"] = activity.TraceId.ToHexString();
            }

            // 执行后续中间件
            await _next(context);

            // 记录响应信息
            if (activity != null)
            {
                activity.SetTag(GatewaySpanAttributes.HttpStatusCode, context.Response.StatusCode);

                if (context.Response.ContentLength.HasValue)
                {
                    activity.SetTag(GatewaySpanAttributes.HttpResponseBodySize, context.Response.ContentLength.Value);
                }

                // 根据状态码设置 Activity 状态
                if (context.Response.StatusCode >= 400)
                {
                    activity.SetStatus(ActivityStatusCode.Error, $"HTTP {context.Response.StatusCode}");
                }
                else
                {
                    activity.SetStatus(ActivityStatusCode.Ok);
                }
            }
        }
        catch (Exception ex)
        {
            // 记录异常
            activity?.RecordException(ex);
            throw;
        }
        finally
        {
            // 停止 Activity 并存储到内存
            if (activity != null)
            {
                activity.Stop();

                // 存储到内存（如果启用）
                if (_traceStore != null && _options.InMemoryStore.Enabled)
                {
                    try
                    {
                        var span = TraceSpan.FromActivity(activity);
                        _traceStore.AddSpan(span);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to store trace span");
                    }
                }

                activity.Dispose();
            }
        }
    }

    private bool ShouldIgnorePath(PathString path)
    {
        if (!path.HasValue)
            return false;

        foreach (var ignorePath in _options.IgnorePaths)
        {
            if (ignorePath.EndsWith('*'))
            {
                // 通配符匹配
                var prefix = ignorePath[..^1];
                if (path.Value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else
            {
                // 精确匹配
                if (path.Value.Equals(ignorePath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
