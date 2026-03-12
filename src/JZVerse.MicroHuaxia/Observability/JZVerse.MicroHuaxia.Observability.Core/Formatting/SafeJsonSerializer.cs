using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JZVerse.MicroHuaxia.Observability.Core.Formatting;

/// <summary>
/// 安全 JSON 序列化器 — 处理循环引用、大对象截断、敏感字段脱敏
/// </summary>
public sealed class SafeJsonSerializer
{
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly int _maxLength;
    private readonly HashSet<string> _sensitiveFields;

    public SafeJsonSerializer(int maxLength, HashSet<string>? sensitiveFields = null)
    {
        _maxLength = maxLength;
        _sensitiveFields = sensitiveFields ?? [];
        _jsonOptions = new JsonSerializerOptions
        {
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            MaxDepth = 5,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false,
        };
    }

    /// <summary>
    /// 安全序列化对象为 JSON 字符串
    /// </summary>
    public string SerializeObject(object? value)
    {
        if (value is null)
            return "null";

        var placeholder = GetPlaceholder(value);
        if (placeholder is not null)
            return placeholder;

        try
        {
            var json = JsonSerializer.Serialize(value, value.GetType(), _jsonOptions);
            json = MaskAndTruncate(json);
            return json;
        }
        catch
        {
            return $"<序列化错误: {value.GetType().Name}>";
        }
    }

    /// <summary>
    /// 对原始 body 字符串做截断和脱敏
    /// </summary>
    public string SerializeBody(string? rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
            return "<empty>";

        return MaskAndTruncate(rawBody);
    }

    private string MaskAndTruncate(string json)
    {
        if (_sensitiveFields.Count > 0)
        {
            json = MaskSensitiveFields(json);
        }

        if (json.Length > _maxLength)
        {
            return string.Concat(json.AsSpan(0, _maxLength), $"...(已截断, 总计: {json.Length} 字符)");
        }

        return json;
    }

    private static string? GetPlaceholder(object value)
    {
        return value switch
        {
            CancellationToken => null,
            Delegate => "\"<Delegate>\"",
            Stream => "\"<Stream>\"",
            byte[] bytes => $"\"<byte[{bytes.Length}]>\"",
            Task => "\"<Task>\"",
            _ => null
        };
    }

    private string MaskSensitiveFields(string json)
    {
        foreach (var field in _sensitiveFields)
        {
            var lowerJson = json.ToLowerInvariant();
            var lowerField = field.ToLowerInvariant();
            var searchPattern = $"\"{lowerField}\"";

            var index = 0;
            while ((index = lowerJson.IndexOf(searchPattern, index, StringComparison.Ordinal)) >= 0)
            {
                var colonIndex = json.IndexOf(':', index + searchPattern.Length);
                if (colonIndex < 0) break;

                var valueStart = colonIndex + 1;
                while (valueStart < json.Length && json[valueStart] == ' ')
                    valueStart++;

                if (valueStart >= json.Length) break;

                if (json[valueStart] == '"')
                {
                    var valueEnd = json.IndexOf('"', valueStart + 1);
                    if (valueEnd > valueStart)
                    {
                        json = string.Concat(json.AsSpan(0, valueStart + 1), "***", json.AsSpan(valueEnd));
                        lowerJson = json.ToLowerInvariant();
                    }
                }

                index = colonIndex + 1;
            }
        }

        return json;
    }
}
