using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ConfigCenter.Abstractions;

/// <summary>
/// 配置项仓储接口
/// </summary>
public interface IConfigItemRepository
{
    /// <summary>
    /// 添加配置项
    /// </summary>
    Task<ConfigItem> AddAsync(ConfigItem item, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新配置项
    /// </summary>
    Task<bool> UpdateAsync(ConfigItem item, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除配置项
    /// </summary>
    Task<bool> RemoveAsync(string itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据 ID 获取配置项
    /// </summary>
    Task<ConfigItem?> GetByIdAsync(string itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据 Key 获取配置项
    /// </summary>
    Task<ConfigItem?> GetByKeyAsync(
        string namespaceId,
        string environmentId,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取命名空间下所有配置项
    /// </summary>
    Task<IReadOnlyList<ConfigItem>> GetByNamespaceAsync(
        string namespaceId,
        string environmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询配置项
    /// </summary>
    Task<IReadOnlyList<ConfigItem>> QueryAsync(
        ConfigQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有配置项
    /// </summary>
    Task<IReadOnlyList<ConfigItem>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取下一个版本号
    /// </summary>
    Task<long> GetNextVersionAsync(string itemId, CancellationToken cancellationToken = default);
}
