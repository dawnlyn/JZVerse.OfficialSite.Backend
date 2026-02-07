using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions;

/// <summary>
/// 服务实例仓储接口（预留持久化扩展）
/// </summary>
public interface IServiceInstanceRepository
{
    /// <summary>
    /// 添加服务实例
    /// </summary>
    /// <param name="instance">服务实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>添加后的服务实例</returns>
    Task<ServiceInstance> AddAsync(ServiceInstance instance, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新服务实例
    /// </summary>
    /// <param name="instance">服务实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否更新成功</returns>
    Task<bool> UpdateAsync(ServiceInstance instance, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除服务实例
    /// </summary>
    /// <param name="instanceId">实例 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否删除成功</returns>
    Task<bool> RemoveAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取服务实例
    /// </summary>
    /// <param name="instanceId">实例 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>服务实例</returns>
    Task<ServiceInstance?> GetByIdAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按服务名称获取实例列表
    /// </summary>
    /// <param name="serviceName">服务名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>服务实例列表</returns>
    Task<IReadOnlyList<ServiceInstance>> GetByServiceNameAsync(
        string serviceName,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 查询服务实例
    /// </summary>
    /// <param name="query">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>服务实例列表</returns>
    Task<IReadOnlyList<ServiceInstance>> QueryAsync(ServiceQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有服务实例
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>服务实例列表</returns>
    Task<IReadOnlyList<ServiceInstance>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有服务名称
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>服务名称列表</returns>
    Task<IReadOnlyList<string>> GetServiceNamesAsync(CancellationToken cancellationToken = default);
}
