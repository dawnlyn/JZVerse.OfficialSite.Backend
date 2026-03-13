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

    // ===== 灰度发布 =====

    /// <summary>
    /// 获取命名空间下活跃的灰度发布列表
    /// </summary>
    Task<List<GrayReleaseInfo>> GetGrayReleasesAsync(string namespaceId, string environmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取灰度发布详情
    /// </summary>
    Task<GrayReleaseInfo?> GetGrayReleaseAsync(string releaseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建灰度发布
    /// </summary>
    Task<GrayReleaseInfo> CreateGrayReleaseAsync(CreateGrayReleaseInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// 开始灰度发布
    /// </summary>
    Task StartGrayReleaseAsync(string releaseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 完成灰度发布
    /// </summary>
    Task CompleteGrayReleaseAsync(string releaseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 回滚灰度发布
    /// </summary>
    Task RollbackGrayReleaseAsync(string releaseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 测试客户端是否匹配灰度规则
    /// </summary>
    Task<ClientMatchTestResult?> TestGrayReleaseMatchAsync(string releaseId, ClientMatchTestInput input, CancellationToken cancellationToken = default);

    // ===== 版本历史 =====

    /// <summary>
    /// 获取配置项版本历史
    /// </summary>
    Task<List<ConfigVersionInfo>> GetConfigVersionHistoryAsync(string itemId, int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// 回滚配置项到指定版本
    /// </summary>
    Task RollbackConfigVersionAsync(string itemId, long targetVersion, string? reason = null, CancellationToken cancellationToken = default);
}
