using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ConfigCenter.Abstractions;

/// <summary>
/// 配置发现接口
/// </summary>
public interface IConfigDiscovery
{
    /// <summary>
    /// 根据查询条件获取配置
    /// </summary>
    Task<Dictionary<string, string>> GetConfigAsync(
        ConfigQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取单个配置值
    /// </summary>
    Task<string?> GetValueAsync(
        string applicationId,
        string environmentId,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询配置项列表
    /// </summary>
    Task<IReadOnlyList<ConfigItem>> QueryAsync(
        ConfigQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取命名空间配置（支持灰度）
    /// </summary>
    Task<Dictionary<string, string>> GetNamespaceConfigAsync(
        string namespaceId,
        string environmentId,
        ClientInfo? clientInfo = null,
        CancellationToken cancellationToken = default);
}
