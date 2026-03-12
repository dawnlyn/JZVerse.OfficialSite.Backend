using System.Diagnostics;
using JZVerse.MicroHuaxia.Observability.Core.Configuration;
using JZVerse.MicroHuaxia.Observability.Core.FileLogging;
using JZVerse.MicroHuaxia.Observability.Core.Formatting;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Observability.AspNetCore.Http;

/// <summary>
/// 出站 HTTP 诊断日志 DelegatingHandler — 发起方打印发出的请求和收到的响应
/// </summary>
public sealed class DiagnosticsLoggingHandler(
    IOptions<ObservabilityOptions> observabilityOptions,
    ConsoleLogFormatter consoleFormatter,
    JsonLogFormatter jsonFormatter,
    IServiceProvider serviceProvider) : DelegatingHandler
{
    private const long MaxBodyReadSize = 10 * 1024 * 1024; // 10MB

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var opts = observabilityOptions.Value;
        var consoleEnabled = opts.Console.Enabled;
        var fileEnabled = opts.File.Enabled;

        if (!consoleEnabled && !fileEnabled)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        // 检查排除路径
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        foreach (var excluded in opts.Console.ExcludedPaths)
        {
            if (path.StartsWith(excluded, StringComparison.OrdinalIgnoreCase))
            {
                return await base.SendAsync(request, cancellationToken);
            }
        }

        // 读取请求体
        string? requestBody = null;
        if ((opts.Console.LogRequestBody || opts.File.Enabled) && request.Content is not null)
        {
            var contentLength = request.Content.Headers.ContentLength;
            if (contentLength is null or <= MaxBodyReadSize)
            {
                requestBody = await request.Content.ReadAsStringAsync(cancellationToken);
                var newContent = new StringContent(requestBody);
                if (request.Content.Headers.ContentType is not null)
                {
                    newContent.Headers.ContentType = request.Content.Headers.ContentType;
                }
                request.Content = newContent;
            }
            else
            {
                requestBody = $"<请求体过大: {contentLength} 字节>";
            }
        }

        // 打印请求行
        var method = request.Method.Method;
        var url = request.RequestUri?.ToString() ?? "<unknown>";

        if (consoleEnabled)
            Console.WriteLine(consoleFormatter.FormatHttpRequest("⟹", method, url, requestBody));

        WriteToFile(opts, consoleFormatter.FormatHttpRequest("⟹", method, url, requestBody),
            jsonFormatter.FormatHttpRequest("⟹", method, url, requestBody));

        var sw = Stopwatch.StartNew();

        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            sw.Stop();

            // 读取响应体
            string? responseBody = null;
            if ((opts.Console.LogResponseBody || opts.File.Enabled) && response.Content is not null)
            {
                var contentLength = response.Content.Headers.ContentLength;
                if (contentLength is null or <= MaxBodyReadSize)
                {
                    await response.Content.LoadIntoBufferAsync();
                    responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                }
                else
                {
                    responseBody = $"<响应体过大: {contentLength} 字节>";
                }
            }

            var statusCode = (int)response.StatusCode;
            var statusText = response.ReasonPhrase ?? response.StatusCode.ToString();

            if (consoleEnabled)
                Console.WriteLine(consoleFormatter.FormatHttpResponse("⟸", statusCode, statusText, sw.ElapsedMilliseconds, responseBody));

            WriteToFile(opts, consoleFormatter.FormatHttpResponse("⟸", statusCode, statusText, sw.ElapsedMilliseconds, responseBody),
                jsonFormatter.FormatHttpResponse("⟸", statusCode, statusText, sw.ElapsedMilliseconds, responseBody));

            return response;
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
        // 移除 ANSI escape sequences: ESC[...m
        return System.Text.RegularExpressions.Regex.Replace(text, @"\x1b\[[0-9;]*m", "");
    }
}
