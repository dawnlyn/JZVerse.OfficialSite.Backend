using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Persistence;

/// <summary>
/// 持久化存储接口（预留）
/// </summary>
public interface IServiceInstanceStore
{
    /// <summary>
    /// 保存快照
    /// </summary>
    /// <param name="instances">服务实例列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SaveSnapshotAsync(IReadOnlyList<ServiceInstance> instances, CancellationToken cancellationToken = default);

    /// <summary>
    /// 加载快照
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>服务实例列表</returns>
    Task<IReadOnlyList<ServiceInstance>> LoadSnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 清除快照
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task ClearSnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 是否存在快照
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否存在</returns>
    Task<bool> ExistsAsync(CancellationToken cancellationToken = default);
}
