using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions;

/// <summary>
/// 健康检查器接口
/// </summary>
public interface IHealthChecker
{
    /// <summary>
    /// 检查器名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 执行健康检查
    /// </summary>
    /// <param name="instance">服务实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>健康检查结果</returns>
    Task<HealthCheckResult> CheckHealthAsync(ServiceInstance instance, CancellationToken cancellationToken = default);

    /// <summary>
    /// 是否支持检查该服务实例
    /// </summary>
    /// <param name="instance">服务实例</param>
    /// <returns>是否支持</returns>
    bool CanCheck(ServiceInstance instance);
}

/// <summary>
/// 健康检查管理器接口
/// </summary>
public interface IHealthCheckManager
{
    /// <summary>
    /// 启动健康检查
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止健康检查
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 注册健康检查器
    /// </summary>
    /// <param name="checker">健康检查器</param>
    void RegisterChecker(IHealthChecker checker);

    /// <summary>
    /// 手动触发健康检查
    /// </summary>
    /// <param name="instanceId">实例 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>健康检查结果</returns>
    Task<HealthCheckResult> CheckInstanceAsync(string instanceId, CancellationToken cancellationToken = default);
}
