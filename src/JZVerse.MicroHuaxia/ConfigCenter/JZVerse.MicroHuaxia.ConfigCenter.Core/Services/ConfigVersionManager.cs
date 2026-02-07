using JZVerse.MicroHuaxia.ConfigCenter.Abstractions;
using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.ConfigCenter.Core.Services;

/// <summary>
/// 配置版本管理器实现
/// </summary>
public class ConfigVersionManager(
    IConfigVersionRepository _versionRepository,
    IConfigItemRepository _itemRepository,
    ILogger<ConfigVersionManager> _logger
) : IConfigVersionManager
{
    public async Task<ConfigVersion> RecordChangeAsync(
        ConfigItem? oldItem,
        ConfigItem newItem,
        string? reason = null,
        string? changedBy = null,
        CancellationToken cancellationToken = default)
    {
        var changeType = oldItem is null
            ? ConfigChangeType.Created
            : ConfigChangeType.Updated;

        var version = new ConfigVersion
        {
            VersionId = Guid.NewGuid().ToString("N"),
            ItemId = newItem.ItemId,
            Version = newItem.Version,
            OldValue = oldItem?.Value,
            NewValue = newItem.Value,
            ChangeType = changeType,
            Reason = reason,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = changedBy,
        };

        await _versionRepository.AddAsync(version, cancellationToken);

        _logger.LogDebug(
            "Recorded version {Version} for config item {ItemId}, change type: {ChangeType}",
            version.Version, newItem.ItemId, changeType);

        return version;
    }

    public Task<IReadOnlyList<ConfigVersion>> GetHistoryAsync(
        string itemId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        return _versionRepository.GetByItemIdAsync(itemId, limit, cancellationToken);
    }

    public async Task<ConfigItem> RollbackAsync(
        string itemId,
        long targetVersion,
        string? rollbackBy = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Rolling back config item {ItemId} to version {TargetVersion}",
            itemId, targetVersion);

        // 获取目标版本
        var targetVersionRecord = await _versionRepository.GetByVersionAsync(
            itemId, targetVersion, cancellationToken);

        if (targetVersionRecord is null)
        {
            throw new InvalidOperationException(
                $"Version {targetVersion} not found for config item {itemId}");
        }

        // 获取当前配置项
        var currentItem = await _itemRepository.GetByIdAsync(itemId, cancellationToken)
            ?? throw new InvalidOperationException($"Config item {itemId} not found");

        // 创建回滚后的配置项
        var nextVersion = await _itemRepository.GetNextVersionAsync(itemId, cancellationToken);
        var rolledBackItem = currentItem with
        {
            Value = targetVersionRecord.NewValue,
            Version = nextVersion,
            UpdatedAt = DateTimeOffset.UtcNow,
            UpdatedBy = rollbackBy,
        };

        await _itemRepository.UpdateAsync(rolledBackItem, cancellationToken);

        // 记录回滚版本
        var rollbackVersion = new ConfigVersion
        {
            VersionId = Guid.NewGuid().ToString("N"),
            ItemId = itemId,
            Version = nextVersion,
            OldValue = currentItem.Value,
            NewValue = targetVersionRecord.NewValue,
            ChangeType = ConfigChangeType.Rollback,
            Reason = $"Rollback from version {currentItem.Version} to version {targetVersion}",
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = rollbackBy,
            RollbackFromVersion = targetVersion,
        };

        await _versionRepository.AddAsync(rollbackVersion, cancellationToken);

        _logger.LogInformation(
            "Rolled back config item {ItemId} to version {TargetVersion}, new version: {NewVersion}",
            itemId, targetVersion, nextVersion);

        return rolledBackItem;
    }

    public Task<ConfigVersion?> GetVersionAsync(
        string itemId,
        long version,
        CancellationToken cancellationToken = default)
    {
        return _versionRepository.GetByVersionAsync(itemId, version, cancellationToken);
    }
}
