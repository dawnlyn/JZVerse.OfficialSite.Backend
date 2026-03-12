using JZVerse.MicroHuaxia.Dashboard.Abstractions.Models;

namespace JZVerse.MicroHuaxia.Dashboard.Abstractions.Services;

/// <summary>
/// 服务发现 API 客户端接口
/// </summary>
public interface IServiceDiscoveryApiClient
{
    /// <summary>
    /// 获取所有服务列表
    /// </summary>
    Task<List<ServiceInfo>> GetServicesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定服务的所有实例
    /// </summary>
    Task<List<ServiceInstance>> GetInstancesAsync(string serviceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取服务发现统计数据
    /// </summary>
    Task<ServiceDiscoveryStats> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 注销服务实例
    /// </summary>
    Task DeregisterInstanceAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新实例健康状态
    /// </summary>
    Task UpdateHealthStatusAsync(string instanceId, HealthStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新实例元数据
    /// </summary>
    Task UpdateMetadataAsync(string instanceId, Dictionary<string, string> metadata, CancellationToken cancellationToken = default);
}
