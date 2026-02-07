using JZVerse.MicroHuaxia.ConfigCenter.Abstractions;
using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.ConfigCenter.Core.Services;

/// <summary>
/// 配置注册服务实现
/// </summary>
public class ConfigRegistry(
    IConfigItemRepository _repository,
    IConfigVersionManager _versionManager,
    IConfigEventPublisher _eventPublisher,
    IConfigCache _cache,
    ILogger<ConfigRegistry> _logger
) : IConfigRegistry
{
    public async Task<ConfigItem> SetAsync(ConfigItem item, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Setting config item {Key} in namespace {NamespaceId}",
            item.Key, item.NamespaceId);

        // 查找是否已存在相同 Key 的配置项
        var existingItem = await _repository.GetByKeyAsync(
            item.NamespaceId, item.EnvironmentId, item.Key, cancellationToken);

        ConfigItem savedItem;
        ConfigChangeType changeType;

        if (existingItem is not null)
        {
            // 更新现有配置项
            var nextVersion = await _repository.GetNextVersionAsync(existingItem.ItemId, cancellationToken);

            // 创建更新后的配置项（保留原有 ItemId）
            savedItem = new ConfigItem
            {
                ItemId = existingItem.ItemId,
                Key = item.Key,
                Value = item.Value,
                ValueType = item.ValueType,
                NamespaceId = item.NamespaceId,
                EnvironmentId = item.EnvironmentId,
                Version = nextVersion,
                Comment = item.Comment,
                IsSecret = item.IsSecret,
                IsRequired = item.IsRequired,
                CreatedAt = existingItem.CreatedAt,
                UpdatedAt = DateTimeOffset.UtcNow,
                CreatedBy = existingItem.CreatedBy,
                UpdatedBy = item.UpdatedBy,
            };

            await _repository.UpdateAsync(savedItem, cancellationToken);
            changeType = ConfigChangeType.Updated;

            _logger.LogInformation(
                "Updated config item {Key} from version {OldVersion} to {NewVersion}",
                item.Key, existingItem.Version, nextVersion);
        }
        else
        {
            // 创建新配置项
            savedItem = item with
            {
                ItemId = string.IsNullOrEmpty(item.ItemId)
                    ? Guid.NewGuid().ToString("N")
                    : item.ItemId,
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            await _repository.AddAsync(savedItem, cancellationToken);
            changeType = ConfigChangeType.Created;

            _logger.LogInformation("Created new config item {Key} with id {ItemId}",
                item.Key, savedItem.ItemId);
        }

        // 记录版本历史
        await _versionManager.RecordChangeAsync(
            existingItem,
            savedItem,
            reason: null,
            changedBy: item.UpdatedBy,
            cancellationToken);

        // 使缓存失效
        await InvalidateCacheAsync(savedItem.NamespaceId, savedItem.EnvironmentId, cancellationToken);

        // 发布变更事件
        await PublishChangeEventAsync(savedItem, changeType, cancellationToken);

        return savedItem;
    }

    public async Task<bool> DeleteAsync(string itemId, CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetByIdAsync(itemId, cancellationToken);
        if (item is null)
        {
            _logger.LogWarning("Config item {ItemId} not found for deletion", itemId);
            return false;
        }

        var result = await _repository.RemoveAsync(itemId, cancellationToken);
        if (!result)
            return false;

        _logger.LogInformation("Deleted config item {Key} ({ItemId})", item.Key, itemId);

        // 记录删除版本
        await _versionManager.RecordChangeAsync(
            item,
            item with { Value = string.Empty },
            reason: "Deleted",
            changedBy: null,
            cancellationToken);

        // 使缓存失效
        await InvalidateCacheAsync(item.NamespaceId, item.EnvironmentId, cancellationToken);

        // 发布变更事件
        await PublishChangeEventAsync(item, ConfigChangeType.Deleted, cancellationToken);

        return true;
    }

    public Task<ConfigItem?> GetAsync(string itemId, CancellationToken cancellationToken = default)
    {
        return _repository.GetByIdAsync(itemId, cancellationToken);
    }

    public Task<IReadOnlyList<ConfigItem>> GetByNamespaceAsync(
        string namespaceId,
        string environmentId,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetByNamespaceAsync(namespaceId, environmentId, cancellationToken);
    }

    public async Task<bool> BatchSetAsync(List<ConfigItem> items, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Batch setting {Count} config items", items.Count);

        foreach (var item in items)
        {
            await SetAsync(item, cancellationToken);
        }

        return true;
    }

    private async Task InvalidateCacheAsync(
        string namespaceId,
        string environmentId,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"config:ns:{namespaceId}:{environmentId}";
        await _cache.InvalidateAsync(cacheKey, cancellationToken);
    }

    private async Task PublishChangeEventAsync(
        ConfigItem item,
        ConfigChangeType changeType,
        CancellationToken cancellationToken)
    {
        var @event = new ConfigChangeEvent
        {
            EventId = Guid.NewGuid().ToString("N"),
            EventType = ConfigEventType.ItemChanged,
            ApplicationId = string.Empty, // TODO: 从命名空间获取应用 ID
            EnvironmentId = item.EnvironmentId,
            NamespaceId = item.NamespaceId,
            ChangedItems = [item],
            Timestamp = DateTimeOffset.UtcNow,
            TriggeredBy = item.UpdatedBy,
            AdditionalData = new Dictionary<string, object>
            {
                ["changeType"] = changeType.ToString(),
            },
        };

        await _eventPublisher.PublishAsync(@event, cancellationToken);
    }
}
