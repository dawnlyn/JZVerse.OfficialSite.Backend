using System.Text.Encodings.Web;
using System.Text.Json;
using JZVerse.MicroHuaxia.Observability.Core.Configuration;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Observability.Core.Formatting;

/// <summary>
/// JSON 格式日志格式化器 — 每条日志输出为单行 JSON 对象，用于文件日志 JSON 模式
/// </summary>
public sealed class JsonLogFormatter
{
    private readonly ConsoleOptions _options;
    private readonly SafeJsonSerializer _serializer;
    private readonly JsonSerializerOptions _jsonOpts;

    public JsonLogFormatter(IOptions<ConsoleOptions> options, SafeJsonSerializer serializer)
    {
        _options = options.Value;
        _serializer = serializer;
        _jsonOpts = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false,
        };
    }

    // ────── HTTP ──────

    public string FormatHttpRequest(string direction, string method, string url, string? body)
    {
        var obj = new Dictionary<string, object?>
        {
            ["ts"] = DateTimeOffset.Now.ToString("o"),
            ["type"] = "HTTP",
            ["dir"] = direction == "⟹" ? "出站" : "入站",
            ["method"] = method,
            ["url"] = url,
        };

        if (_options.LogRequestBody && !string.IsNullOrWhiteSpace(body))
            obj["body"] = _serializer.SerializeBody(body);

        return JsonSerializer.Serialize(obj, _jsonOpts);
    }

    public string FormatHttpResponse(string direction, int statusCode, string statusText, long elapsedMs, string? body)
    {
        var obj = new Dictionary<string, object?>
        {
            ["ts"] = DateTimeOffset.Now.ToString("o"),
            ["type"] = "HTTP",
            ["dir"] = direction == "⟹" ? "出站" : "入站",
            ["status"] = statusCode,
            ["statusText"] = statusText,
            ["elapsed"] = elapsedMs,
        };

        if (_options.LogResponseBody && !string.IsNullOrWhiteSpace(body))
            obj["body"] = _serializer.SerializeBody(body);

        return JsonSerializer.Serialize(obj, _jsonOpts);
    }

    public string FormatHttpError(long elapsedMs, Exception error)
    {
        var obj = new Dictionary<string, object?>
        {
            ["ts"] = DateTimeOffset.Now.ToString("o"),
            ["type"] = "HTTP",
            ["level"] = "Error",
            ["elapsed"] = elapsedMs,
            ["error"] = error.GetType().Name,
            ["message"] = error.Message,
        };

        return JsonSerializer.Serialize(obj, _jsonOpts);
    }

    // ────── gRPC ──────

    public string FormatGrpcRequest(string direction, string methodFullName, string? requestJson)
    {
        var obj = new Dictionary<string, object?>
        {
            ["ts"] = DateTimeOffset.Now.ToString("o"),
            ["type"] = "gRPC",
            ["dir"] = direction == "⟹" ? "出站" : "入站",
            ["method"] = methodFullName,
        };

        if (_options.LogRequestBody && !string.IsNullOrWhiteSpace(requestJson))
            obj["body"] = _serializer.SerializeBody(requestJson);

        return JsonSerializer.Serialize(obj, _jsonOpts);
    }

    public string FormatGrpcResponse(string direction, string statusCode, long elapsedMs, string? responseJson)
    {
        var obj = new Dictionary<string, object?>
        {
            ["ts"] = DateTimeOffset.Now.ToString("o"),
            ["type"] = "gRPC",
            ["dir"] = direction == "⟹" ? "出站" : "入站",
            ["status"] = statusCode,
            ["elapsed"] = elapsedMs,
        };

        if (_options.LogResponseBody && !string.IsNullOrWhiteSpace(responseJson))
            obj["body"] = _serializer.SerializeBody(responseJson);

        return JsonSerializer.Serialize(obj, _jsonOpts);
    }

    public string FormatGrpcError(long elapsedMs, Exception error)
    {
        var obj = new Dictionary<string, object?>
        {
            ["ts"] = DateTimeOffset.Now.ToString("o"),
            ["type"] = "gRPC",
            ["level"] = "Error",
            ["elapsed"] = elapsedMs,
            ["error"] = error.GetType().Name,
            ["message"] = error.Message,
        };

        return JsonSerializer.Serialize(obj, _jsonOpts);
    }

    // ────── JSON-RPC ──────

    public string FormatJsonRpcRequest(string direction, string method, string? serviceName, string? paramsJson)
    {
        var obj = new Dictionary<string, object?>
        {
            ["ts"] = DateTimeOffset.Now.ToString("o"),
            ["type"] = "JSON-RPC",
            ["dir"] = direction == "⟹" ? "出站" : "入站",
            ["method"] = method,
        };

        if (!string.IsNullOrWhiteSpace(serviceName))
            obj["service"] = serviceName;

        if (_options.LogRequestBody && !string.IsNullOrWhiteSpace(paramsJson))
            obj["params"] = _serializer.SerializeBody(paramsJson);

        return JsonSerializer.Serialize(obj, _jsonOpts);
    }

    public string FormatJsonRpcResponse(string direction, long elapsedMs, string? resultJson, string? errorJson)
    {
        var isError = !string.IsNullOrWhiteSpace(errorJson);

        var obj = new Dictionary<string, object?>
        {
            ["ts"] = DateTimeOffset.Now.ToString("o"),
            ["type"] = "JSON-RPC",
            ["dir"] = direction == "⟹" ? "出站" : "入站",
            ["status"] = isError ? "错误" : "成功",
            ["elapsed"] = elapsedMs,
        };

        if (isError)
            obj["error"] = _serializer.SerializeBody(errorJson);
        else if (_options.LogResponseBody && !string.IsNullOrWhiteSpace(resultJson))
            obj["result"] = _serializer.SerializeBody(resultJson);

        return JsonSerializer.Serialize(obj, _jsonOpts);
    }
}
