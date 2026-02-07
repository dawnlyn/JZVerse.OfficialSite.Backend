using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.LoadBalancing;
using ServiceInstance = JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models.ServiceInstance;

namespace JZVerse.MicroHuaxia.ServiceCommunication.Core.LoadBalancing;

/// <summary>
/// 负载均衡器工厂实现
/// </summary>
public sealed class LoadBalancerFactory : ILoadBalancerFactory
{
    private readonly ConcurrentDictionary<string, ILoadBalancer> _loadBalancers = new();
    private readonly Dictionary<LoadBalancerStrategy, Func<ILoadBalancer>> _factories;
    private readonly IInstanceMetricsCollector? _metricsCollector;

    public LoadBalancerFactory(IInstanceMetricsCollector? metricsCollector = null)
    {
        _metricsCollector = metricsCollector;

        _factories = new()
        {
            [LoadBalancerStrategy.RoundRobin] = () => new RoundRobinLoadBalancer(),
            [LoadBalancerStrategy.Random] = () => new RandomLoadBalancer(),
            [LoadBalancerStrategy.WeightedRandom] = () => new WeightedRandomLoadBalancer(),
            [LoadBalancerStrategy.ConsistentHash] = () => new ConsistentHashLoadBalancer(),
            [LoadBalancerStrategy.WeightedRoundRobin] = () => new WeightedRoundRobinLoadBalancer(),
            [LoadBalancerStrategy.LeastConnections] = () => new LeastConnectionsLoadBalancer(),
            [LoadBalancerStrategy.IpHash] = () => new ConsistentHashLoadBalancer(), // 复用一致性哈希
            [LoadBalancerStrategy.AdaptiveWeighted] = CreateAdaptiveWeightedLoadBalancer,
        };
    }

    private ILoadBalancer CreateAdaptiveWeightedLoadBalancer()
    {
        if (_metricsCollector is not null)
        {
            return new AdaptiveWeightedLoadBalancer(_metricsCollector);
        }

        // 如果没有指标收集器，回退到加权轮询
        return new WeightedRoundRobinLoadBalancer();
    }

    public ILoadBalancer GetOrCreate(string serviceName, LoadBalancerStrategy strategy = LoadBalancerStrategy.RoundRobin)
    {
        var key = $"{serviceName}:{strategy}";
        return _loadBalancers.GetOrAdd(key, _ =>
            _factories.TryGetValue(strategy, out var factory)
                ? factory()
                : new RoundRobinLoadBalancer());
    }
}

/// <summary>
/// 加权轮询负载均衡器
/// </summary>
public sealed class WeightedRoundRobinLoadBalancer : ILoadBalancer
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, int> _currentWeights = new();
    private int _effectiveWeight;

    public string Name => "WeightedRoundRobin";

    public ServiceInstance? Select(IReadOnlyList<ServiceInstance> instances, LoadBalancerContext? context = null)
    {
        if (instances.Count == 0) return null;

        var filtered = FilterInstances(instances, context);
        if (filtered.Count == 0) return null;

        lock (_lock)
        {
            ServiceInstance? selected = null;
            var maxWeight = int.MinValue;

            foreach (var instance in filtered)
            {
                var currentWeight = _currentWeights.GetValueOrDefault(instance.InstanceId) + instance.Weight;
                _currentWeights[instance.InstanceId] = currentWeight;

                if (currentWeight > maxWeight)
                {
                    maxWeight = currentWeight;
                    selected = instance;
                }
            }

            if (selected is not null)
            {
                _effectiveWeight = filtered.Sum(i => i.Weight);
                _currentWeights[selected.InstanceId] -= _effectiveWeight;
            }

            return selected;
        }
    }

    private static IReadOnlyList<ServiceInstance> FilterInstances(
        IReadOnlyList<ServiceInstance> instances,
        LoadBalancerContext? context)
    {
        if (context is null) return instances;

        var filtered = instances.AsEnumerable();

        if (context.ExcludedInstanceIds is { Count: > 0 })
        {
            filtered = filtered.Where(i => !context.ExcludedInstanceIds.Contains(i.InstanceId));
        }

        return filtered.ToList();
    }
}

/// <summary>
/// 最少连接负载均衡器（简化实现，基于权重反向）
/// </summary>
public sealed class LeastConnectionsLoadBalancer : ILoadBalancer
{
    private readonly ConcurrentDictionary<string, int> _connectionCounts = new();

    public string Name => "LeastConnections";

    public ServiceInstance? Select(IReadOnlyList<ServiceInstance> instances, LoadBalancerContext? context = null)
    {
        if (instances.Count == 0) return null;

        var filtered = FilterInstances(instances, context);
        if (filtered.Count == 0) return null;

        return filtered
            .OrderBy(i => _connectionCounts.GetValueOrDefault(i.InstanceId))
            .ThenByDescending(i => i.Weight)
            .FirstOrDefault();
    }

    /// <summary>
    /// 增加连接计数
    /// </summary>
    public void IncrementConnection(string instanceId)
    {
        _connectionCounts.AddOrUpdate(instanceId, 1, (_, c) => c + 1);
    }

    /// <summary>
    /// 减少连接计数
    /// </summary>
    public void DecrementConnection(string instanceId)
    {
        _connectionCounts.AddOrUpdate(instanceId, 0, (_, c) => Math.Max(0, c - 1));
    }

    private static IReadOnlyList<ServiceInstance> FilterInstances(
        IReadOnlyList<ServiceInstance> instances,
        LoadBalancerContext? context)
    {
        if (context is null) return instances;

        var filtered = instances.AsEnumerable();

        if (context.ExcludedInstanceIds is { Count: > 0 })
        {
            filtered = filtered.Where(i => !context.ExcludedInstanceIds.Contains(i.InstanceId));
        }

        return filtered.ToList();
    }
}
