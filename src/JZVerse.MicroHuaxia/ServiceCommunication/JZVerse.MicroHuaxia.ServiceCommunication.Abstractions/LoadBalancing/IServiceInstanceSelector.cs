using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.LoadBalancing;

/// <summary>
/// 负载均衡器工厂
/// </summary>
public interface ILoadBalancerFactory
{
    /// <summary>
    /// 获取或创建负载均衡器
    /// </summary>
    /// <param name="serviceName">服务名称</param>
    /// <param name="strategy">负载均衡策略</param>
    ILoadBalancer GetOrCreate(string serviceName, LoadBalancerStrategy strategy = LoadBalancerStrategy.RoundRobin);
}

/// <summary>
/// 服务实例选择器 - 集成服务发现和负载均衡
/// </summary>
public interface IServiceInstanceSelector
{
    /// <summary>
    /// 选择一个服务实例
    /// </summary>
    /// <param name="serviceName">服务名称</param>
    /// <param name="context">负载均衡上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<ServiceInstance?> SelectAsync(
        string serviceName,
        LoadBalancerContext? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取服务的所有可用实例
    /// </summary>
    /// <param name="serviceName">服务名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<ServiceInstance>> GetInstancesAsync(
        string serviceName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 报告实例状态（用于熔断/降级）
    /// </summary>
    /// <param name="instance">实例</param>
    /// <param name="success">是否成功</param>
    /// <param name="duration">耗时</param>
    /// <param name="exception">异常信息</param>
    void ReportInstanceStatus(
        ServiceInstance instance,
        bool success,
        TimeSpan duration,
        Exception? exception = null);
}
