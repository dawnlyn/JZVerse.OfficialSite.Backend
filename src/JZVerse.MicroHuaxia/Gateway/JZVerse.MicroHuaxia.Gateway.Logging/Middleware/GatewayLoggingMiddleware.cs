using System.Diagnostics;
using JZVerse.MicroHuaxia.Gateway.Logging.Configuration;
using JZVerse.MicroHuaxia.Gateway.Logging.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Gateway.Logging.Middleware;

/// <summary>
/// Gateway 日志记录中间件
/// </summary>
public sealed class GatewayLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly GatewayLoggingOptions _options;
    private readonly ILogger<GatewayLoggingMiddleware> _logger;

    /// <summary>
    /// 创建日志记录中间件
    /// </summary>
    public GatewayLoggingMiddleware(
        RequestDelegate next,
        IOptions<GatewayLoggingOptions> options,
        ILogger<GatewayLoggingMiddleware> logger)
    {
        _next = next;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 处理请求
    /// </summary>
    public async Task InvokeAsync(HttpContext context, ILogStore logStore)
    {
        // 检查是否应该忽略此路径
        if (ShouldIgnore(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var requestId = context.TraceIdentifier;
        var traceId = Activity.Current?.TraceId.ToString();
        var spanId = Activity.Current?.SpanId.ToString();

        // 记录请求开始
        var requestEntry = new LogEntry
        {
            Level = LogLevel.Information,
            Category = "Gateway.Middleware.Request",
            Message = $"HTTP {context.Request.Method} {context.Request.Path} 开始处理",
            TraceId = traceId,
            SpanId = spanId,
            ServiceName = "Gateway.Middleware",
            RequestPath = context.Request.Path,
            RequestMethod = context.Request.Method,
            RequestId = requestId,
            ClientIp = GetClientIp(context),
            Source = LogSource.Middleware
        };

        await logStore.AddAsync(requestEntry);

        Exception? exception = null;
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            stopwatch.Stop();

            // 记录请求完成
            var responseEntry = new LogEntry
            {
                Level = exception is not null ? LogLevel.Error : LogLevel.Information,
                Category = "Gateway.Middleware.Response",
                Message = exception is not null
                    ? $"HTTP {context.Request.Method} {context.Request.Path} 处理失败: {exception.Message}"
                    : $"HTTP {context.Request.Method} {context.Request.Path} -> {context.Response.StatusCode} ({stopwatch.ElapsedMilliseconds}ms)",
                Exception = exception?.ToString(),
                TraceId = traceId,
                SpanId = spanId,
                ServiceName = "Gateway.Middleware",
                RequestPath = context.Request.Path,
                RequestMethod = context.Request.Method,
                RequestId = requestId,
                ClientIp = GetClientIp(context),
                StatusCode = context.Response.StatusCode,
                DurationMs = stopwatch.Elapsed.TotalMilliseconds,
                Source = LogSource.Middleware
            };

            await logStore.AddAsync(responseEntry);
        }
    }

    private bool ShouldIgnore(PathString path)
    {
        foreach (var ignorePath in _options.IgnorePaths)
        {
            if (path.StartsWithSegments(ignorePath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static string? GetClientIp(HttpContext context)
    {
        // 尝试从 X-Forwarded-For 头获取
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',').FirstOrDefault()?.Trim();
        }

        // 尝试从 X-Real-IP 头获取
        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        // 使用连接的远程 IP
        return context.Connection.RemoteIpAddress?.ToString();
    }
}
