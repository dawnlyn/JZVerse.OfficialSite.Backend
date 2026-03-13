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

    /// <summary>
    /// 长轮询等待配置变更
    /// </summary>
    /// <returns>如果有变更则返回新版本号，无变更返回 null</returns>
    Task<ConfigWatchResult?> WatchAsync(
        string namespaceId,
        string environmentId,
        long lastVersion,
        int timeoutSeconds = 30,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 配置监听结果
/// </summary>
public class ConfigWatchResult
{
    /// <summary>
    /// 是否有变更
    /// </summary>
    public bool HasChanged { get; set; }

    /// <summary>
    /// 最新版本号
    /// </summary>
    public long Version { get; set; }

    /// <summary>
    /// 变更后的配置（仅 HasChanged=true 时有值）
    /// </summary>
    public Dictionary<string, string>? Config { get; set; }
}
