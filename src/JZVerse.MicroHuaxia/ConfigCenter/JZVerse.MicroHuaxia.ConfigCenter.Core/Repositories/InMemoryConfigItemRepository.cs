using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.ConfigCenter.Abstractions;
using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ConfigCenter.Core.Repositories;

/// <summary>
/// 内存配置项仓储实现
/// </summary>
public class InMemoryConfigItemRepository : IConfigItemRepository
{
    private readonly ConcurrentDictionary<string, ConfigItem> _items = new();

    // 命名空间+环境索引：{namespaceId}:{environmentId} -> Set<itemId>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _namespaceIndex = new();

    // Key 索引：{namespaceId}:{environmentId}:{key} -> itemId
    private readonly ConcurrentDictionary<string, string> _keyIndex = new();

    private readonly Lock _indexLock = new();

    public Task<ConfigItem> AddAsync(ConfigItem item, CancellationToken cancellationToken = default)
    {
        _items[item.ItemId] = item;
        UpdateIndexes(item);
        return Task.FromResult(item);
    }

    public Task<bool> UpdateAsync(ConfigItem item, CancellationToken cancellationToken = default)
    {
        if (!_items.ContainsKey(item.ItemId))
            return Task.FromResult(false);

        _items[item.ItemId] = item;
        return Task.FromResult(true);
    }

    public Task<bool> RemoveAsync(string itemId, CancellationToken cancellationToken = default)
    {
        if (!_items.TryRemove(itemId, out var item))
            return Task.FromResult(false);

        RemoveFromIndexes(item);
        return Task.FromResult(true);
    }

    public Task<ConfigItem?> GetByIdAsync(string itemId, CancellationToken cancellationToken = default)
    {
        _items.TryGetValue(itemId, out var item);
        return Task.FromResult(item);
    }

    public Task<ConfigItem?> GetByKeyAsync(
        string namespaceId,
        string environmentId,
        string key,
        CancellationToken cancellationToken = default)
    {
        var keyIndexKey = BuildKeyIndexKey(namespaceId, environmentId, key);
        if (_keyIndex.TryGetValue(keyIndexKey, out var itemId))
        {
            _items.TryGetValue(itemId, out var item);
            return Task.FromResult(item);
        }
        return Task.FromResult<ConfigItem?>(null);
    }

    public Task<IReadOnlyList<ConfigItem>> GetByNamespaceAsync(
        string namespaceId,
        string environmentId,
        CancellationToken cancellationToken = default)
    {
        var indexKey = BuildNamespaceIndexKey(namespaceId, environmentId);
        if (_namespaceIndex.TryGetValue(indexKey, out var itemIds))
        {
            var items = itemIds.Keys
                .Select(id => _items.GetValueOrDefault(id))
                .Where(item => item is not null)
                .Cast<ConfigItem>()
                .ToList();
            return Task.FromResult<IReadOnlyList<ConfigItem>>(items);
        }
        return Task.FromResult<IReadOnlyList<ConfigItem>>([]);
    }

    public Task<IReadOnlyList<ConfigItem>> QueryAsync(
        ConfigQuery query,
        CancellationToken cancellationToken = default)
    {
        var items = _items.Values.AsEnumerable();

        // 按命名空间过滤
        if (!string.IsNullOrEmpty(query.NamespaceId))
        {
            items = items.Where(i => i.NamespaceId == query.NamespaceId);
        }

        // 按环境过滤
        items = items.Where(i => i.EnvironmentId == query.EnvironmentId);

        // 按 Key 列表过滤
        if (query.Keys is { Count: > 0 })
        {
            var keySet = query.Keys.ToHashSet();
            items = items.Where(i => keySet.Contains(i.Key));
        }

        // 是否包含敏感配置
        if (!query.IncludeSecrets)
        {
            items = items.Where(i => !i.IsSecret);
        }

        return Task.FromResult<IReadOnlyList<ConfigItem>>(items.ToList());
    }

    public Task<IReadOnlyList<ConfigItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ConfigItem>>(_items.Values.ToList());
    }

    public Task<long> GetNextVersionAsync(string itemId, CancellationToken cancellationToken = default)
    {
        if (_items.TryGetValue(itemId, out var item))
        {
            return Task.FromResult(item.Version + 1);
        }
        return Task.FromResult(1L);
    }

    private void UpdateIndexes(ConfigItem item)
    {
        lock (_indexLock)
        {
            // 更新命名空间索引
            var nsIndexKey = BuildNamespaceIndexKey(item.NamespaceId, item.EnvironmentId);
            var itemSet = _namespaceIndex.GetOrAdd(nsIndexKey, _ => new ConcurrentDictionary<string, byte>());
            itemSet[item.ItemId] = 0;

            // 更新 Key 索引
            var keyIndexKey = BuildKeyIndexKey(item.NamespaceId, item.EnvironmentId, item.Key);
            _keyIndex[keyIndexKey] = item.ItemId;
        }
    }

    private void RemoveFromIndexes(ConfigItem item)
    {
        lock (_indexLock)
        {
            // 从命名空间索引移除
            var nsIndexKey = BuildNamespaceIndexKey(item.NamespaceId, item.EnvironmentId);
            if (_namespaceIndex.TryGetValue(nsIndexKey, out var itemSet))
            {
                itemSet.TryRemove(item.ItemId, out _);
            }

            // 从 Key 索引移除
            var keyIndexKey = BuildKeyIndexKey(item.NamespaceId, item.EnvironmentId, item.Key);
            _keyIndex.TryRemove(keyIndexKey, out _);
        }
    }

    private static string BuildNamespaceIndexKey(string namespaceId, string environmentId)
        => $"{namespaceId}:{environmentId}";

    private static string BuildKeyIndexKey(string namespaceId, string environmentId, string key)
        => $"{namespaceId}:{environmentId}:{key}";
}
