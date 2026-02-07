using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions;

/// <summary>
/// 服务发现接口
/// </summary>
public interface IServiceDiscovery
{
    /// <summary>
    /// 根据条件查询服务实例列表
    /// </summary>
    /// <param name="query">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>服务实例列表</returns>
    Task<IReadOnlyList<ServiceInstance>> DiscoverAsync(
        ServiceQuery query,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 按服务名称获取所有实例
    /// </summary>
    /// <param name="serviceName">服务名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>服务实例列表</returns>
    Task<IReadOnlyList<ServiceInstance>> GetInstancesAsync(
        string serviceName,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 获取单个服务实例（负载均衡）
    /// </summary>
    /// <param name="serviceName">服务名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>单个服务实例</returns>
    Task<ServiceInstance?> GetInstanceAsync(string serviceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定实例详情
    /// </summary>
    /// <param name="instanceId">实例 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>服务实例</returns>
    Task<ServiceInstance?> GetInstanceByIdAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有服务名称
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>服务名称列表</returns>
    Task<IReadOnlyList<string>> GetServiceNamesAsync(CancellationToken cancellationToken = default);
}
