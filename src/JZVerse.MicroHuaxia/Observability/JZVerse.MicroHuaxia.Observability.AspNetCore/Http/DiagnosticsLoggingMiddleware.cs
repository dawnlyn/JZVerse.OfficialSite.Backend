using System.Diagnostics;
using JZVerse.MicroHuaxia.Observability.Core.Configuration;
using JZVerse.MicroHuaxia.Observability.Core.FileLogging;
using JZVerse.MicroHuaxia.Observability.Core.Formatting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Observability.AspNetCore.Http;

/// <summary>
/// 入站 HTTP 诊断日志中间件 — 接收方打印收到的请求和返回的响应
/// </summary>
public sealed class DiagnosticsLoggingMiddleware(
    RequestDelegate next,
    IOptions<ObservabilityOptions> observabilityOptions,
    ConsoleLogFormatter consoleFormatter,
    JsonLogFormatter jsonFormatter,
    IServiceProvider serviceProvider)
{
    private const long MaxBodyReadSize = 10 * 1024 * 1024; // 10MB

    public async Task InvokeAsync(HttpContext context)
    {
        var opts = observabilityOptions.Value;
        var consoleEnabled = opts.Console.Enabled;
        var fileEnabled = opts.File.Enabled;

        if (!consoleEnabled && !fileEnabled)
        {
            await next(context);
            return;
        }

        // 检查排除路径
        var path = context.Request.Path.Value ?? string.Empty;
        foreach (var excluded in opts.Console.ExcludedPaths)
        {
            if (path.StartsWith(excluded, StringComparison.OrdinalIgnoreCase))
            {
                await next(context);
                return;
            }
        }

        // 读取请求体
        string? requestBody = null;
        if (opts.Console.LogRequestBody || fileEnabled)
        {
            context.Request.EnableBuffering();
            var contentLength = context.Request.ContentLength;
            if (contentLength is null or <= MaxBodyReadSize)
            {
                using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                requestBody = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;
            }
            else
            {
                requestBody = $"<请求体过大: {contentLength} 字节>";
            }
        }

        // 打印收到的请求行
        var method = context.Request.Method;
        var requestPath = $"{context.Request.Path}{context.Request.QueryString}";

        if (consoleEnabled)
            Console.WriteLine(consoleFormatter.FormatHttpRequest("⟸", method, requestPath, requestBody));

        WriteToFile(opts, consoleFormatter.FormatHttpRequest("⟸", method, requestPath, requestBody),
            jsonFormatter.FormatHttpRequest("⟸", method, requestPath, requestBody));

        var sw = Stopwatch.StartNew();

        // 替换 Response.Body 以便捕获响应体
        var originalResponseBody = context.Response.Body;
        using var responseBodyStream = new MemoryStream();
        context.Response.Body = responseBodyStream;

        try
        {
            await next(context);
            sw.Stop();

            // 读取响应体
            string? responseBody = null;
            if (opts.Console.LogResponseBody || fileEnabled)
            {
                responseBodyStream.Position = 0;
                if (responseBodyStream.Length <= MaxBodyReadSize)
                {
                    using var reader = new StreamReader(responseBodyStream, leaveOpen: true);
                    responseBody = await reader.ReadToEndAsync();
                }
                else
                {
                    responseBody = $"<body too large: {responseBodyStream.Length} bytes>";
                }
            }

            // 复制响应体回原始 stream
            responseBodyStream.Position = 0;
            await responseBodyStream.CopyToAsync(originalResponseBody);

            var statusCode = context.Response.StatusCode;
            var statusText = GetReasonPhrase(statusCode);

            if (consoleEnabled)
                Console.WriteLine(consoleFormatter.FormatHttpResponse("⟹", statusCode, statusText, sw.ElapsedMilliseconds, responseBody));

            WriteToFile(opts, consoleFormatter.FormatHttpResponse("⟹", statusCode, statusText, sw.ElapsedMilliseconds, responseBody),
                jsonFormatter.FormatHttpResponse("⟹", statusCode, statusText, sw.ElapsedMilliseconds, responseBody));
        }
        catch (Exception ex)
        {
            sw.Stop();

            if (consoleEnabled)
                Console.WriteLine(consoleFormatter.FormatHttpError(sw.ElapsedMilliseconds, ex));

            WriteToFile(opts, consoleFormatter.FormatHttpError(sw.ElapsedMilliseconds, ex),
                jsonFormatter.FormatHttpError(sw.ElapsedMilliseconds, ex));

            throw;
        }
        finally
        {
            context.Response.Body = originalResponseBody;
        }
    }

    private void WriteToFile(ObservabilityOptions opts, string textLine, string jsonLine)
    {
        if (!opts.File.Enabled) return;

        var writer = serviceProvider.GetService(typeof(FileLogWriter)) as FileLogWriter;
        if (writer is null) return;

        var line = opts.File.Format == FileLogFormat.Json ? jsonLine : StripAnsiColors(textLine);
        writer.WriteLine(line);
    }

    private static string StripAnsiColors(string text)
    {
        return System.Text.RegularExpressions.Regex.Replace(text, @"\x1b\[[0-9;]*m", "");
    }

    private static string GetReasonPhrase(int statusCode) => statusCode switch
    {
        200 => "成功",
        201 => "已创建",
        204 => "无内容",
        301 => "永久重定向",
        302 => "临时重定向",
        304 => "未修改",
        400 => "请求无效",
        401 => "未授权",
        403 => "禁止访问",
        404 => "未找到",
        405 => "方法不允许",
        409 => "冲突",
        422 => "无法处理",
        429 => "请求过多",
        500 => "服务器内部错误",
        502 => "网关错误",
        503 => "服务不可用",
        504 => "网关超时",
        _ => statusCode.ToString(),
    };
}
