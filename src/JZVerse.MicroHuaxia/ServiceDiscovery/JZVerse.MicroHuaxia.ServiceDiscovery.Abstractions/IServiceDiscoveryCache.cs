using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions;

/// <summary>
/// 服务发现缓存接口
/// </summary>
public interface IServiceDiscoveryCache
{
    /// <summary>
    /// 是否启用缓存
    /// </summary>
    bool IsCacheEnabled { get; }

    /// <summary>
    /// 获取缓存的服务实例列表
    /// </summary>
    /// <param name="serviceName">服务名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>服务实例列表，不存在则返回 null</returns>
    Task<IReadOnlyList<ServiceInstance>?> GetCachedInstancesAsync(
        string serviceName,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 更新缓存
    /// </summary>
    /// <param name="serviceName">服务名称</param>
    /// <param name="instances">服务实例列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SetCachedInstancesAsync(
        string serviceName,
        IReadOnlyList<ServiceInstance> instances,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 清除缓存
    /// </summary>
    /// <param name="serviceName">服务名称,为空则清除所有</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ClearCacheAsync(string? serviceName = null, CancellationToken cancellationToken = default);
}
