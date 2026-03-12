using System.Text;
using JZVerse.MicroHuaxia.Observability.Core.Configuration;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Observability.Core.Formatting;

/// <summary>
/// 通信层诊断日志格式化器 — 鲜艳 ANSI 彩色输出
/// </summary>
public sealed class ConsoleLogFormatter
{
    private readonly ConsoleOptions _options;
    private readonly SafeJsonSerializer _serializer;

    public ConsoleLogFormatter(IOptions<ConsoleOptions> options, SafeJsonSerializer serializer)
    {
        _options = options.Value;
        _serializer = serializer;
    }

    // ────── HTTP ──────

    /// <summary>
    /// 格式化 HTTP 请求行
    /// </summary>
    /// <param name="direction">⟹ (发出) 或 ⟸ (接收)</param>
    public string FormatHttpRequest(string direction, string method, string url, string? body)
    {
        var sb = new StringBuilder(256);
        var c = _options.EnableColors;
        var ts = DateTime.Now.ToString("HH:mm:ss.fff");

        sb.Append(C(c, AnsiColors.BrightWhite, $"[{ts}]"));
        sb.Append(' ');
        sb.Append(C(c, AnsiColors.BrightBlue, direction));
        sb.Append(' ');
        sb.Append(C(c, AnsiColors.BrightCyan, "HTTP"));
        sb.Append("  ");
        sb.Append(C(c, AnsiColors.BoldWhite, method));
        sb.Append(' ');
        sb.Append(C(c, AnsiColors.BrightWhite, url));

        if (_options.LogRequestBody && !string.IsNullOrWhiteSpace(body))
        {
            var bodyStr = _serializer.SerializeBody(body);
            sb.AppendLine();
            sb.Append(C(c, AnsiColors.BrightWhite, "  └── 请求体: "));
            sb.Append(C(c, AnsiColors.BrightMagenta, bodyStr));
        }

        return sb.ToString();
    }

    /// <summary>
    /// 格式化 HTTP 响应行
    /// </summary>
    public string FormatHttpResponse(string direction, int statusCode, string statusText, long elapsedMs, string? body)
    {
        var sb = new StringBuilder(256);
        var c = _options.EnableColors;
        var ts = DateTime.Now.ToString("HH:mm:ss.fff");
        var statusColor = GetStatusColor(statusCode);
        var durationColor = GetDurationColor(elapsedMs);

        sb.Append(C(c, AnsiColors.BrightWhite, $"[{ts}]"));
        sb.Append(' ');
        sb.Append(C(c, AnsiColors.BrightBlue, direction));
        sb.Append(' ');
        sb.Append(C(c, AnsiColors.BrightCyan, "HTTP"));
        sb.Append("  ");
        sb.Append(C(c, statusColor, $"{statusCode} {statusText}"));
        sb.Append("  ");
        sb.Append(C(c, AnsiColors.BrightWhite, "("));
        sb.Append(C(c, durationColor, $"{elapsedMs}ms"));
        sb.Append(C(c, AnsiColors.BrightWhite, ")"));

        if (_options.LogResponseBody && !string.IsNullOrWhiteSpace(body))
        {
            var bodyStr = _serializer.SerializeBody(body);
            sb.AppendLine();
            sb.Append(C(c, AnsiColors.BrightWhite, "  └── 响应体: "));
            sb.Append(C(c, AnsiColors.BrightMagenta, bodyStr));
        }

        return sb.ToString();
    }

    /// <summary>
    /// 格式化 HTTP 错误行
    /// </summary>
    public string FormatHttpError(long elapsedMs, Exception error)
    {
        var sb = new StringBuilder(256);
        var c = _options.EnableColors;
        var ts = DateTime.Now.ToString("HH:mm:ss.fff");
        var durationColor = GetDurationColor(elapsedMs);

        sb.Append(C(c, AnsiColors.BrightWhite, $"[{ts}]"));
        sb.Append(' ');
        sb.Append(C(c, AnsiColors.BrightRed, "✗"));
        sb.Append(' ');
        sb.Append(C(c, AnsiColors.BrightCyan, "HTTP"));
        sb.Append("  ");
        sb.Append(C(c, AnsiColors.BrightRed, "异常"));
        sb.Append("  ");
        sb.Append(C(c, AnsiColors.BrightWhite, "("));
        sb.Append(C(c, durationColor, $"{elapsedMs}ms"));
        sb.Append(C(c, AnsiColors.BrightWhite, ")"));

        sb.AppendLine();
        sb.Append(C(c, AnsiColors.BrightWhite, "  └── 错误: "));
        sb.Append(C(c, AnsiColors.BrightRed, $"{error.GetType().Name}: {error.Message}"));

        return sb.ToString();
    }

    // ────── gRPC ──────

    public string FormatGrpcRequest(string direction, string methodFullName, string? requestJson)
    {
        var sb = new StringBuilder(256);
        var c = _options.EnableColors;
        var ts = DateTime.Now.ToString("HH:mm:ss.fff");

        sb.Append(C(c, AnsiColors.BrightWhite, $"[{ts}]"));
        sb.Append(' ');
        sb.Append(C(c, AnsiColors.BrightBlue, direction));
        sb.Append(' ');
        sb.Append(C(c, AnsiColors.BrightCyan, "gRPC"));
        sb.Append("  ");
        sb.Append(C(c, AnsiColors.BoldWhite, methodFullName));

        if (_options.LogRequestBody && !string.IsNullOrWhiteSpace(requestJson))
        {
            var bodyStr = _serializer.SerializeBody(requestJson);
            sb.AppendLine();
            sb.Append(C(c, AnsiColors.BrightWhite, "  └── Request: "));
            sb.Append(C(c, AnsiColors.BrightMagenta, bodyStr));
        }

        return sb.ToString();
    }

    public string FormatGrpcResponse(string direction, string statusCode, long elapsedMs, string? responseJson)
    {
        var sb = new StringBuilder(256);
        var c = _options.EnableColors;
        var ts = DateTime.Now.ToString("HH:mm:ss.fff");
        var statusColor = statusCode == "OK" || statusCode == "成功" ? AnsiColors.BrightGreen : AnsiColors.BrightRed;
        var durationColor = GetDurationColor(elapsedMs);

        sb.Append(C(c, AnsiColors.BrightWhite, $"[{ts}]"));
        sb.Append(' ');
        sb.Append(C(c, AnsiColors.BrightBlue, direction));
        sb.Append(' ');
        sb.Append(C(c, AnsiColors.BrightCyan, "gRPC"));
        sb.Append("  ");
        sb.Append(C(c, statusColor, statusCode));
        sb.Append("  ");
        sb.Append(C(c, AnsiColors.BrightWhite, "("));
        sb.Append(C(c, durationColor, $"{elapsedMs}ms"));
        sb.Append(C(c, AnsiColors.BrightWhite, ")"));

        if (_options.LogResponseBody && !string.IsNullOrWhiteSpace(responseJson))
        {
            var bodyStr = _serializer.SerializeBody(responseJson);
            sb.AppendLine();
            sb.Append(C(c, AnsiColors.BrightWhite, "  └── 响应: "));
            sb.Append(C(c, AnsiColors.BrightMagenta, bodyStr));
        }

        return sb.ToString();
    }

    public string FormatGrpcError(long elapsedMs, Exception error)
    {
        var sb = new StringBuilder(256);
        var c = _options.EnableColors;
        var ts = DateTime.Now.ToString("HH:mm:ss.fff");
        var durationColor = GetDurationColor(elapsedMs);

        sb.Append(C(c, AnsiColors.BrightWhite, $"[{ts}]"));
        sb.Append(' ');
        sb.Append(C(c, AnsiColors.BrightRed, "✗"));
        sb.Append(' ');
        sb.Append(C(c, AnsiColors.BrightCyan, "gRPC"));
        sb.Append("  ");
        sb.Append(C(c, AnsiColors.BrightRed, "异常"));
        sb.Append("  ");
        sb.Append(C(c, AnsiColors.BrightWhite, "("));
        sb.Append(C(c, durationColor, $"{elapsedMs}ms"));
        sb.Append(C(c, AnsiColors.BrightWhite, ")"));

        sb.AppendLine();
        sb.Append(C(c, AnsiColors.BrightWhite, "  └── 错误: "));
        sb.Append(C(c, AnsiColors.BrightRed, $"{error.GetType().Name}: {error.Message}"));

        return sb.ToString();
    }

    // ────── JSON-RPC ──────

    public string FormatJsonRpcRequest(string direction, string method, string? serviceName, string? paramsJson)
    {
        var sb = new StringBuilder(256);
        var c = _options.EnableColors;
        var ts = DateTime.Now.ToString("HH:mm:ss.fff");

        sb.Append(C(c, AnsiColors.BrightWhite, $"[{ts}]"));
        sb.Append(' ');
        sb.Append(C(c, AnsiColors.BrightBlue, direction));
        sb.Append(' ');
        sb.Append(C(c, AnsiColors.BrightCyan, "JSON-RPC"));
        sb.Append("  ");
        sb.Append(C(c, AnsiColors.BoldWhite, method));

        if (!string.IsNullOrWhiteSpace(serviceName))
        {
            sb.Append(C(c, AnsiColors.BrightWhite, " → "));
            sb.Append(C(c, AnsiColors.BrightWhite, serviceName));
        }

        if (_options.LogRequestBody && !string.IsNullOrWhiteSpace(paramsJson))
        {
            var bodyStr = _serializer.SerializeBody(paramsJson);
            sb.AppendLine();
            sb.Append(C(c, AnsiColors.BrightWhite, "  └── 参数: "));
            sb.Append(C(c, AnsiColors.BrightMagenta, bodyStr));
        }

        return sb.ToString();
    }

    public string FormatJsonRpcResponse(string direction, long elapsedMs, string? resultJson, string? errorJson)
    {
        var sb = new StringBuilder(256);
        var c = _options.EnableColors;
        var ts = DateTime.Now.ToString("HH:mm:ss.fff");
        var isError = !string.IsNullOrWhiteSpace(errorJson);
        var statusColor = isError ? AnsiColors.BrightRed : AnsiColors.BrightGreen;
        var statusText = isError ? "错误" : "成功";
        var durationColor = GetDurationColor(elapsedMs);

        sb.Append(C(c, AnsiColors.BrightWhite, $"[{ts}]"));
        sb.Append(' ');
        sb.Append(C(c, AnsiColors.BrightBlue, direction));
        sb.Append(' ');
        sb.Append(C(c, AnsiColors.BrightCyan, "JSON-RPC"));
        sb.Append("  ");
        sb.Append(C(c, statusColor, statusText));
        sb.Append("  ");
        sb.Append(C(c, AnsiColors.BrightWhite, "("));
        sb.Append(C(c, durationColor, $"{elapsedMs}ms"));
        sb.Append(C(c, AnsiColors.BrightWhite, ")"));

        if (isError)
        {
            var bodyStr = _serializer.SerializeBody(errorJson);
            sb.AppendLine();
            sb.Append(C(c, AnsiColors.BrightWhite, "  └── 错误: "));
            sb.Append(C(c, AnsiColors.BrightRed, bodyStr));
        }
        else if (_options.LogResponseBody && !string.IsNullOrWhiteSpace(resultJson))
        {
            var bodyStr = _serializer.SerializeBody(resultJson);
            sb.AppendLine();
            sb.Append(C(c, AnsiColors.BrightWhite, "  └── 结果: "));
            sb.Append(C(c, AnsiColors.BrightMagenta, bodyStr));
        }

        return sb.ToString();
    }

    // ────── 辅助方法 ──────

    private string GetStatusColor(int statusCode) => statusCode switch
    {
        >= 200 and < 300 => AnsiColors.BrightGreen,
        >= 300 and < 400 => AnsiColors.BrightYellow,
        >= 400 and < 500 => AnsiColors.BrightYellow,
        _ => AnsiColors.BrightRed,
    };

    private string GetDurationColor(long elapsedMs)
    {
        if (elapsedMs < 100) return AnsiColors.BrightGreen;
        if (elapsedMs < _options.SlowCallThresholdMs) return AnsiColors.BrightYellow;
        return AnsiColors.BrightRed;
    }

    /// <summary>
    /// 条件着色：启用颜色时包裹 ANSI 码，否则原样返回
    /// </summary>
    private static string C(bool enableColors, string colorCode, string text)
    {
        return enableColors ? $"{colorCode}{text}{AnsiColors.Reset}" : text;
    }
}
