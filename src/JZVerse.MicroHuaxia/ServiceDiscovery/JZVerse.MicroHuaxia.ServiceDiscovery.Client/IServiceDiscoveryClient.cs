using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Client;

/// <summary>
/// 服务发现客户端接口
/// </summary>
public interface IServiceDiscoveryClient
{
    /// <summary>
    /// 注册服务实例
    /// </summary>
    Task<ServiceInstance> RegisterAsync(
        ServiceRegistration registration,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 注销服务实例
    /// </summary>
    Task<bool> DeregisterAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送心跳
    /// </summary>
    Task<bool> HeartbeatAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取服务实例列表
    /// </summary>
    Task<IReadOnlyList<ServiceInstance>> GetInstancesAsync(
        string serviceName,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 获取单个服务实例（负载均衡）
    /// </summary>
    Task<ServiceInstance?> GetInstanceAsync(string serviceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有服务名称
    /// </summary>
    Task<IReadOnlyList<string>> GetServiceNamesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据条件查询服务
    /// </summary>
    Task<IReadOnlyList<ServiceInstance>> DiscoverAsync(
        ServiceQuery query,
        CancellationToken cancellationToken = default
    );
}
