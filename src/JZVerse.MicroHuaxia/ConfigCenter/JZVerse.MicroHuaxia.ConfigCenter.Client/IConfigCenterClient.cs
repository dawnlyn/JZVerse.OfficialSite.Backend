using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ConfigCenter.Client;

/// <summary>
/// 配置中心客户端接口
/// </summary>
public interface IConfigCenterClient
{
    /// <summary>
    /// 获取命名空间配置
    /// </summary>
    Task<Dictionary<string, string>> GetConfigAsync(
        string namespaceId,
        string environmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取单个配置值
    /// </summary>
    Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取配置值并转换为指定类型
    /// </summary>
    Task<T?> GetValueAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 订阅配置变更
    /// </summary>
    Task SubscribeAsync(
        string namespaceId,
        Func<ConfigChangeEvent, Task> callback,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取消订阅
    /// </summary>
    Task UnsubscribeAsync(string namespaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 强制刷新配置
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);
}
