using System.Text.Json;
using JZVerse.MicroHuaxia.DataAccess.Abstractions.Caching;

namespace JZVerse.MicroHuaxia.DataAccess.Core.Caching.Serialization;

/// <summary>
/// JSON 缓存序列化器
/// </summary>
public sealed class JsonCacheSerializer : ICacheSerializer
{
    private readonly JsonSerializerOptions _options;

    public JsonCacheSerializer()
    {
        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    public JsonCacheSerializer(JsonSerializerOptions options)
    {
        _options = options;
    }

    /// <inheritdoc />
    public byte[] Serialize<T>(T value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value, _options);
    }

    /// <inheritdoc />
    public T? Deserialize<T>(byte[] data)
    {
        return JsonSerializer.Deserialize<T>(data, _options);
    }

    /// <inheritdoc />
    public string SerializeToString<T>(T value)
    {
        return JsonSerializer.Serialize(value, _options);
    }

    /// <inheritdoc />
    public T? DeserializeFromString<T>(string data)
    {
        return JsonSerializer.Deserialize<T>(data, _options);
    }
}
