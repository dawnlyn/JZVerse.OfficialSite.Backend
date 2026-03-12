using JZVerse.MicroHuaxia.Dashboard.Abstractions.Models;

namespace JZVerse.MicroHuaxia.Dashboard.Abstractions.Services;

/// <summary>
/// 配置中心 API 客户端接口
/// </summary>
public interface IConfigCenterApiClient
{
    /// <summary>
    /// 获取配置中心统计数据
    /// </summary>
    Task<ConfigCenterStats> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有命名空间
    /// </summary>
    Task<List<ConfigNamespace>> GetNamespacesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取命名空间下的配置项
    /// </summary>
    Task<List<ConfigItem>> GetConfigItemsAsync(string namespaceId, string? environmentId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取单个配置项
    /// </summary>
    Task<ConfigItem?> GetConfigItemAsync(string itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建或更新配置项
    /// </summary>
    Task<ConfigItem> UpsertConfigItemAsync(ConfigItem item, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除配置项
    /// </summary>
    Task DeleteConfigItemAsync(string itemId, CancellationToken cancellationToken = default);
}
