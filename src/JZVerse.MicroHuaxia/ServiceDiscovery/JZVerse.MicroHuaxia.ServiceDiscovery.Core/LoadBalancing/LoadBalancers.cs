using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Core.LoadBalancing;

/// <summary>
/// 轮询负载均衡器
/// </summary>
public class RoundRobinLoadBalancer : ILoadBalancer
{
    private readonly Dictionary<string, int> _counters = new();
    private readonly Lock _lock = new();

    /// <inheritdoc />
    public string Name => "RoundRobin";

    /// <inheritdoc />
    public ServiceInstance? Select(IReadOnlyList<ServiceInstance> instances)
    {
        switch (instances.Count)
        {
            case 0:
                return null;
            case 1:
                return instances[0];
        }

        lock (_lock)
        {
            var serviceName = instances[0].ServiceName;
            var counter = _counters.GetValueOrDefault(serviceName, 0);

            var index = counter % instances.Count;
            _counters[serviceName] = counter + 1;

            return instances[index];
        }
    }
}

/// <summary>
/// 随机负载均衡器
/// </summary>
public class RandomLoadBalancer : ILoadBalancer
{
    private static readonly Random Random = new();

    /// <inheritdoc />
    public string Name => "Random";

    /// <inheritdoc />
    public ServiceInstance? Select(IReadOnlyList<ServiceInstance> instances)
    {
        switch (instances.Count)
        {
            case 0:
                return null;
            case 1:
                return instances[0];
            default:
            {
                var index = Random.Next(instances.Count);
                return instances[index];
            }
        }
    }
}

/// <summary>
/// 加权随机负载均衡器
/// </summary>
public class WeightedRandomLoadBalancer : ILoadBalancer
{
    private static readonly Random Random = new();

    /// <inheritdoc />
    public string Name => "WeightedRandom";

    /// <inheritdoc />
    public ServiceInstance? Select(IReadOnlyList<ServiceInstance> instances)
    {
        switch (instances.Count)
        {
            case 0:
                return null;
            case 1:
                return instances[0];
        }

        var totalWeight = instances.Sum(i => i.Weight);
        if (totalWeight <= 0)
        {
            // 如果所有权重都是0，回退到随机选择
            return instances[Random.Next(instances.Count)];
        }

        var randomWeight = Random.Next(totalWeight);

        var currentWeight = 0;
        foreach (var instance in instances.OrderBy(i => i.Weight))
        {
            currentWeight += instance.Weight;
            if (randomWeight < currentWeight)
            {
                return instance;
            }
        }

        return instances[^1];
    }
}

/// <summary>
/// 加权轮询负载均衡器
/// </summary>
public class WeightedRoundRobinLoadBalancer : ILoadBalancer
{
    private readonly Dictionary<string, int> _counters = new();
    private readonly Lock _lock = new();

    /// <inheritdoc />
    public string Name => "WeightedRoundRobin";

    /// <inheritdoc />
    public ServiceInstance? Select(IReadOnlyList<ServiceInstance> instances)
    {
        switch (instances.Count)
        {
            case 0:
                return null;
            case 1:
                return instances[0];
        }

        lock (_lock)
        {
            var serviceName = instances[0].ServiceName;
            var counter = _counters.GetValueOrDefault(serviceName, 0);

            // 构建加权列表
            var weightedList = new List<ServiceInstance>();
            foreach (var instance in instances)
            {
                var weight = Math.Max(1, instance.Weight / 10); // 简化权重
                for (var i = 0; i < weight; i++)
                {
                    weightedList.Add(instance);
                }
            }

            if (weightedList.Count == 0)
            {
                return instances[counter % instances.Count];
            }

            var index = counter % weightedList.Count;
            _counters[serviceName] = counter + 1;

            return weightedList[index];
        }
    }
}
