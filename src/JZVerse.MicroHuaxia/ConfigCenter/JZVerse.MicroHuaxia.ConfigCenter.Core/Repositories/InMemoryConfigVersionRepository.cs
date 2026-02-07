using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.ConfigCenter.Abstractions;
using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ConfigCenter.Core.Repositories;

/// <summary>
/// 内存配置版本仓储实现
/// </summary>
public class InMemoryConfigVersionRepository : IConfigVersionRepository
{
    // itemId -> List<ConfigVersion>（按版本号降序）
    private readonly ConcurrentDictionary<string, List<ConfigVersion>> _versions = new();
    private readonly Lock _lock = new();

    public Task<ConfigVersion> AddAsync(ConfigVersion version, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var versions = _versions.GetOrAdd(version.ItemId, _ => []);
            versions.Insert(0, version); // 最新版本在前
        }
        return Task.FromResult(version);
    }

    public Task<IReadOnlyList<ConfigVersion>> GetByItemIdAsync(
        string itemId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (_versions.TryGetValue(itemId, out var versions))
        {
            lock (_lock)
            {
                return Task.FromResult<IReadOnlyList<ConfigVersion>>(
                    versions.Take(limit).ToList());
            }
        }
        return Task.FromResult<IReadOnlyList<ConfigVersion>>([]);
    }

    public Task<ConfigVersion?> GetByVersionAsync(
        string itemId,
        long version,
        CancellationToken cancellationToken = default)
    {
        if (_versions.TryGetValue(itemId, out var versions))
        {
            lock (_lock)
            {
                var found = versions.FirstOrDefault(v => v.Version == version);
                return Task.FromResult(found);
            }
        }
        return Task.FromResult<ConfigVersion?>(null);
    }
}
