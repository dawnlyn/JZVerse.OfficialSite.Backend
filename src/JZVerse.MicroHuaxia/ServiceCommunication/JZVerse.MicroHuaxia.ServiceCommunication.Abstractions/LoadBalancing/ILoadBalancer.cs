using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.LoadBalancing;

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
    /// <param name="context">请求上下文</param>
    ServiceInstance? Select(IReadOnlyList<ServiceInstance> instances, LoadBalancerContext? context = null);
}

/// <summary>
/// 负载均衡上下文
/// </summary>
public sealed record LoadBalancerContext
{
    /// <summary>
    /// 请求标识（用于一致性哈希）
    /// </summary>
    public string? RequestId { get; init; }

    /// <summary>
    /// 客户端标识（用于会话粘滞）
    /// </summary>
    public string? ClientId { get; init; }

    /// <summary>
    /// 请求的 Key（用于一致性哈希）
    /// </summary>
    public string? HashKey { get; init; }

    /// <summary>
    /// 优先选择的版本
    /// </summary>
    public string? PreferredVersion { get; init; }

    /// <summary>
    /// 优先选择的标签
    /// </summary>
    public HashSet<string>? PreferredTags { get; init; }

    /// <summary>
    /// 排除的实例 ID（已尝试失败的实例）
    /// </summary>
    public HashSet<string>? ExcludedInstanceIds { get; init; }
}

/// <summary>
/// 负载均衡策略
/// </summary>
public enum LoadBalancerStrategy
{
    /// <summary>
    /// 轮询
    /// </summary>
    RoundRobin,

    /// <summary>
    /// 随机
    /// </summary>
    Random,

    /// <summary>
    /// 加权轮询
    /// </summary>
    WeightedRoundRobin,

    /// <summary>
    /// 加权随机
    /// </summary>
    WeightedRandom,

    /// <summary>
    /// 最少连接
    /// </summary>
    LeastConnections,

    /// <summary>
    /// 一致性哈希
    /// </summary>
    ConsistentHash,

    /// <summary>
    /// IP 哈希
    /// </summary>
    IpHash,

    /// <summary>
    /// 自适应加权（基于响应时间和成功率动态调整）
    /// </summary>
    AdaptiveWeighted
}
