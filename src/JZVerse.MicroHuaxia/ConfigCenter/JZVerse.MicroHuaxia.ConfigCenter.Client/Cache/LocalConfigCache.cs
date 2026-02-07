using System.Collections.Concurrent;
using System.Text.Json;

namespace JZVerse.MicroHuaxia.ConfigCenter.Client.Cache;

/// <summary>
/// 本地配置缓存
/// </summary>
public class LocalConfigCache(string cachePath)
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly string _cachePath = cachePath;

    /// <summary>
    /// 获取缓存的配置
    /// </summary>
    public Dictionary<string, string>? Get(string namespaceId, string environmentId)
    {
        var key = BuildKey(namespaceId, environmentId);
        if (_cache.TryGetValue(key, out var entry))
        {
            return entry.Config;
        }
        return null;
    }

    /// <summary>
    /// 设置缓存
    /// </summary>
    public void Set(string namespaceId, string environmentId, Dictionary<string, string> config, long version)
    {
        var key = BuildKey(namespaceId, environmentId);
        _cache[key] = new CacheEntry
        {
            Config = new Dictionary<string, string>(config),
            Version = version,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>
    /// 获取版本号
    /// </summary>
    public long GetVersion(string namespaceId, string environmentId)
    {
        var key = BuildKey(namespaceId, environmentId);
        if (_cache.TryGetValue(key, out var entry))
        {
            return entry.Version;
        }
        return 0;
    }

    /// <summary>
    /// 持久化缓存到文件
    /// </summary>
    public async Task PersistAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_cachePath))
        {
            Directory.CreateDirectory(_cachePath);
        }

        var snapshot = new CacheSnapshot
        {
            Entries = _cache.ToDictionary(kv => kv.Key, kv => kv.Value),
            Timestamp = DateTimeOffset.UtcNow,
        };

        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
        var filePath = Path.Combine(_cachePath, "config-cache.json");
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    /// <summary>
    /// 从文件加载缓存
    /// </summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(_cachePath, "config-cache.json");
        if (!File.Exists(filePath))
            return;

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var snapshot = JsonSerializer.Deserialize<CacheSnapshot>(json);

        if (snapshot?.Entries is not null)
        {
            foreach (var (key, entry) in snapshot.Entries)
            {
                _cache[key] = entry;
            }
        }
    }

    /// <summary>
    /// 清除缓存
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
    }

    private static string BuildKey(string namespaceId, string environmentId)
        => $"{namespaceId}:{environmentId}";

    private sealed class CacheEntry
    {
        public Dictionary<string, string> Config { get; init; } = new();
        public long Version { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }
    }

    private sealed class CacheSnapshot
    {
        public Dictionary<string, CacheEntry> Entries { get; init; } = new();
        public DateTimeOffset Timestamp { get; init; }
    }
}
