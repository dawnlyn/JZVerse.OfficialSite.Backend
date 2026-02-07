using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions;

/// <summary>
/// 负载均衡器接口
/// </summary>
public interface ILoadBalancer
{
    /// <summary>
    /// 策略名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 从实例列表中选择一个实例
    /// </summary>
    /// <param name="instances">可用实例列表</param>
    /// <returns>选中的实例</returns>
    ServiceInstance? Select(IReadOnlyList<ServiceInstance> instances);
}
