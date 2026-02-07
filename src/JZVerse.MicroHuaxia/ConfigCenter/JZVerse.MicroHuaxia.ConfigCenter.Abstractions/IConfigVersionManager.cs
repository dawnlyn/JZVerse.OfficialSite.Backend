using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ConfigCenter.Abstractions;

/// <summary>
/// 配置版本管理接口
/// </summary>
public interface IConfigVersionManager
{
    /// <summary>
    /// 记录配置变更
    /// </summary>
    Task<ConfigVersion> RecordChangeAsync(
        ConfigItem? oldItem,
        ConfigItem newItem,
        string? reason = null,
        string? changedBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取配置项版本历史
    /// </summary>
    Task<IReadOnlyList<ConfigVersion>> GetHistoryAsync(
        string itemId,
        int limit = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 回滚配置到指定版本
    /// </summary>
    Task<ConfigItem> RollbackAsync(
        string itemId,
        long targetVersion,
        string? rollbackBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定版本详情
    /// </summary>
    Task<ConfigVersion?> GetVersionAsync(
        string itemId,
        long version,
        CancellationToken cancellationToken = default);
}
