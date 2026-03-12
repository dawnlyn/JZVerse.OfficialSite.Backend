using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions;

/// <summary>
/// 服务注册接口
/// </summary>
public interface IServiceRegistry
{
    /// <summary>
    /// 注册服务实例
    /// </summary>
    /// <param name="registration">注册信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>注册成功的服务实例</returns>
    Task<ServiceInstance> RegisterAsync(
        ServiceRegistration registration,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 注销服务实例
    /// </summary>
    /// <param name="instanceId">实例 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否注销成功</returns>
    Task<bool> DeregisterAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送心跳
    /// </summary>
    /// <param name="instanceId">实例 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否心跳成功</returns>
    Task<bool> HeartbeatAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新服务健康状态
    /// </summary>
    /// <param name="instanceId">实例 ID</param>
    /// <param name="status">健康状态</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否更新成功</returns>
    Task<bool> UpdateHealthStatusAsync(
        string instanceId,
        HealthStatus status,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 更新服务元数据
    /// </summary>
    /// <param name="instanceId">实例 ID</param>
    /// <param name="metadata">新的元数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否更新成功</returns>
    Task<bool> UpdateMetadataAsync(
        string instanceId,
        ServiceMetadata metadata,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 永久删除已注销的服务实例
    /// </summary>
    /// <param name="instanceId">实例 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否删除成功</returns>
    Task<bool> PurgeAsync(string instanceId, CancellationToken cancellationToken = default);
}
