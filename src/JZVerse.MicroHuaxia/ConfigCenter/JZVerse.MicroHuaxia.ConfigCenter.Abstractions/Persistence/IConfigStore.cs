namespace JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Persistence;

/// <summary>
/// 配置持久化存储接口
/// </summary>
public interface IConfigStore
{
    /// <summary>
    /// 保存配置快照
    /// </summary>
    Task SaveSnapshotAsync(
        string namespaceId,
        string environmentId,
        Dictionary<string, string> config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 加载配置快照
    /// </summary>
    Task<Dictionary<string, string>> LoadSnapshotAsync(
        string namespaceId,
        string environmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 清除配置快照
    /// </summary>
    Task ClearSnapshotAsync(
        string namespaceId,
        string environmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查快照是否存在
    /// </summary>
    Task<bool> ExistsAsync(
        string namespaceId,
        string environmentId,
        CancellationToken cancellationToken = default);
}
