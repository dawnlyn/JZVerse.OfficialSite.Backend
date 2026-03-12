using System.Diagnostics;
using System.Text.Json;
using JZVerse.Business.Abstractions.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JZVerse.Business.Infrastructure.Middleware;

/// <summary>
/// 统一异常处理中间件
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// HTTP 499 状态码（客户端关闭连接）- 非标准状态码
    /// </summary>
    private const int Status499ClientClosedRequest = 499;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        var requestId = context.TraceIdentifier;

        // 根据异常类型确定响应
        var (statusCode, errorCode, message) = exception switch
        {
            ValidationException ve => (StatusCodes.Status400BadRequest, ve.ErrorCode, ve.UserMessage),
            AuthenticationException ae => (StatusCodes.Status401Unauthorized, ae.ErrorCode, ae.UserMessage),
            PermissionDeniedException pe => (StatusCodes.Status403Forbidden, pe.ErrorCode, pe.UserMessage),
            ResourceNotFoundException re => (StatusCodes.Status404NotFound, re.ErrorCode, re.UserMessage),
            BusinessException be => (StatusCodes.Status400BadRequest, be.ErrorCode, be.UserMessage),
            OperationCanceledException => (Status499ClientClosedRequest, 499, "请求已取消"),
            _ => (StatusCodes.Status500InternalServerError, 500, "服务器内部错误")
        };

        // 记录日志
        if (statusCode >= 500)
        {
            _logger.LogError(exception, "服务器错误 - TraceId: {TraceId}, Path: {Path}",
                traceId, context.Request.Path);
        }
        else if (statusCode >= 400)
        {
            _logger.LogWarning("业务异常 - TraceId: {TraceId}, Path: {Path}, Message: {Message}",
                traceId, context.Request.Path, message);
        }

        // 构建响应
        var response = new ApiResponse
        {
            Code = errorCode,
            Message = _environment.IsDevelopment() && statusCode >= 500
                ? $"{message}: {exception.Message}"
                : message,
            TraceId = traceId,
            RequestId = requestId
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }
}
