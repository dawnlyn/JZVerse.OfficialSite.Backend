using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ConfigCenter.Abstractions;

/// <summary>
/// 配置注册管理接口
/// </summary>
public interface IConfigRegistry
{
    /// <summary>
    /// 设置配置项（创建或更新）
    /// </summary>
    Task<ConfigItem> SetAsync(ConfigItem item, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除配置项
    /// </summary>
    Task<bool> DeleteAsync(string itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取配置项
    /// </summary>
    Task<ConfigItem?> GetAsync(string itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取命名空间下所有配置项
    /// </summary>
    Task<IReadOnlyList<ConfigItem>> GetByNamespaceAsync(
        string namespaceId,
        string environmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量设置配置项
    /// </summary>
    Task<bool> BatchSetAsync(List<ConfigItem> items, CancellationToken cancellationToken = default);
}
