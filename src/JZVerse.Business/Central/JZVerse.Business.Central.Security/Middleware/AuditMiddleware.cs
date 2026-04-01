using System.Diagnostics;
using System.Text.Json;
using JZVerse.Business.Central.Security.Services.Interfaces;

namespace JZVerse.Business.Central.Security.Middleware;

/// <summary>
/// 审计日志中间件
/// </summary>
public class AuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAuditService _auditService;
    private readonly ILogger<AuditMiddleware> _logger;
    private readonly AuditMiddlewareOptions _options;

    public AuditMiddleware(
        RequestDelegate next,
        IAuditService auditService,
        ILogger<AuditMiddleware> logger,
        AuditMiddlewareOptions options)
    {
        _next = next;
        _auditService = auditService;
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

        var stopwatch = Stopwatch.StartNew();
        var originalBodyStream = context.Response.Body;
        
        // 捕获请求数据
        var requestData = await CaptureRequestDataAsync(context.Request);
        
        // 创建内存流用于捕获响应
        using var responseBodyStream = new MemoryStream();
        context.Response.Body = responseBodyStream;

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

            // 捕获响应数据
            var responseData = await CaptureResponseDataAsync(context.Response, responseBodyStream, originalBodyStream);

            // 记录审计日志
            await LogAuditAsync(context, requestData, responseData, stopwatch.ElapsedMilliseconds, exception);
        }
    }

    private async Task<AuditRequestData> CaptureRequestDataAsync(HttpRequest request)
    {
        var data = new AuditRequestData
        {
            Method = request.Method,
            Path = request.Path.ToString(),
            QueryString = request.QueryString.ToString(),
            Headers = _options.CaptureRequestHeaders 
                ? request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()) 
                : null
        };

        // 捕获请求体
        if (_options.CaptureRequestBody && request.ContentLength > 0 && request.ContentLength < _options.MaxBodySize)
        {
            request.EnableBuffering();
            using var reader = new StreamReader(request.Body, leaveOpen: true);
            data.Body = await reader.ReadToEndAsync();
            request.Body.Position = 0;

            // 尝试解析为对象
            if (!string.IsNullOrEmpty(data.Body))
            {
                try
                {
                    data.BodyObject = JsonSerializer.Deserialize<object>(data.Body);
                }
                catch
                {
                    // 解析失败，保持原始字符串
                }
            }
        }

        return data;
    }

    private async Task<AuditResponseData> CaptureResponseDataAsync(HttpResponse response, MemoryStream captureStream, Stream originalStream)
    {
        var data = new AuditResponseData
        {
            StatusCode = response.StatusCode,
            Headers = _options.CaptureResponseHeaders 
                ? response.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()) 
                : null
        };

        // 复制响应内容到原始流
        captureStream.Position = 0;
        await captureStream.CopyToAsync(originalStream);
        response.Body = originalStream;

        // 捕获响应体
        if (_options.CaptureResponseBody && captureStream.Length > 0 && captureStream.Length < _options.MaxBodySize)
        {
            captureStream.Position = 0;
            using var reader = new StreamReader(captureStream);
            data.Body = await reader.ReadToEndAsync();

            // 只记录JSON响应的部分内容
            if (!string.IsNullOrEmpty(data.Body) && data.Body.Length > 1000)
            {
                data.Body = data.Body.Substring(0, 1000) + "... [truncated]";
            }
        }

        return data;
    }

    private async Task LogAuditAsync(HttpContext context, AuditRequestData requestData, AuditResponseData responseData, long durationMs, Exception? exception)
    {
        try
        {
            // 获取用户信息
            var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var username = context.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

            // 判断是否敏感操作
            var isSensitive = IsSensitiveOperation(context.Request.Method, requestData.Path);

            // 判断操作类型
            var operationType = GetOperationType(context.Request.Method);
            var module = GetModuleFromPath(requestData.Path);

            // 构建审计条目
            var entry = new AuditLogEntry
            {
                UserId = string.IsNullOrEmpty(userId) ? null : Guid.Parse(userId),
                Username = username,
                OperationType = operationType,
                Module = module,
                OperationContent = $"{requestData.Method} {requestData.Path}",
                RequestData = _options.CaptureRequestBody ? requestData.BodyObject : null,
                ResponseData = _options.CaptureResponseBody && responseData.StatusCode >= 400 ? responseData.Body : null,
                Result = exception != null ? "Failure" : (responseData.StatusCode < 400 ? "Success" : "Failure"),
                ErrorMessage = exception?.Message,
                IpAddress = GetClientIpAddress(context),
                UserAgent = context.Request.Headers.UserAgent.ToString(),
                RequestPath = requestData.Path,
                HttpMethod = requestData.Method,
                DurationMs = durationMs,
                IsSensitive = isSensitive
            };

            // 异步记录
            await _auditService.LogAsync(entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record audit log");
        }
    }

    private bool IsSensitiveOperation(string method, string path)
    {
        // DELETE 操作都是敏感的
        if (method == "DELETE")
            return true;

        // POST/PUT 操作包含敏感关键词
        if ((method == "POST" || method == "PUT") && _options.SensitivePatterns.Any(p => path.Contains(p, StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }

    private string GetOperationType(string method)
    {
        return method.ToUpper() switch
        {
            "GET" => "Query",
            "POST" => "Create",
            "PUT" => "Update",
            "PATCH" => "Update",
            "DELETE" => "Delete",
            _ => "Other"
        };
    }

    private string GetModuleFromPath(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 2)
        {
            return segments[1]; // /api/v1/module/action
        }
        return "Unknown";
    }

    private string GetClientIpAddress(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

/// <summary>
/// 审计中间件选项
/// </summary>
public class AuditMiddlewareOptions
{
    /// <summary>
    /// 白名单路径
    /// </summary>
    public List<string> WhiteListPaths { get; set; } = new()
    {
        "/health",
        "/swagger",
        "/favicon.ico"
    };

    /// <summary>
    /// 敏感路径模式
    /// </summary>
    public List<string> SensitivePatterns { get; set; } = new()
    {
        "password",
        "auth",
        "login",
        "config",
        "secret",
        "token"
    };

    /// <summary>
    /// 是否捕获请求头
    /// </summary>
    public bool CaptureRequestHeaders { get; set; } = false;

    /// <summary>
    /// 是否捕获请求体
    /// </summary>
    public bool CaptureRequestBody { get; set; } = true;

    /// <summary>
    /// 是否捕获响应头
    /// </summary>
    public bool CaptureResponseHeaders { get; set; } = false;

    /// <summary>
    /// 是否捕获响应体
    /// </summary>
    public bool CaptureResponseBody { get; set; } = false;

    /// <summary>
    /// 最大捕获体大小（字节）
    /// </summary>
    public int MaxBodySize { get; set; } = 10240; // 10KB
}

/// <summary>
/// 审计请求数据
/// </summary>
public class AuditRequestData
{
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string QueryString { get; set; } = string.Empty;
    public Dictionary<string, string>? Headers { get; set; }
    public string? Body { get; set; }
    public object? BodyObject { get; set; }
}

/// <summary>
/// 审计响应数据
/// </summary>
public class AuditResponseData
{
    public int StatusCode { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
    public string? Body { get; set; }
}
