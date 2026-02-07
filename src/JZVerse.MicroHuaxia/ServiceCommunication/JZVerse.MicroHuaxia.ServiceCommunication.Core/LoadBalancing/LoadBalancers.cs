using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.LoadBalancing;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ServiceCommunication.Core.LoadBalancing;

/// <summary>
/// 轮询负载均衡器
/// </summary>
public sealed class RoundRobinLoadBalancer : ILoadBalancer
{
    private readonly Lock _lock = new();
    private int _index;

    public string Name => "RoundRobin";

    public ServiceInstance? Select(IReadOnlyList<ServiceInstance> instances, LoadBalancerContext? context = null)
    {
        if (instances.Count == 0) return null;

        var filtered = FilterInstances(instances, context);
        if (filtered.Count == 0) return null;

        lock (_lock)
        {
            _index = (_index + 1) % filtered.Count;
            return filtered[_index];
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

        if (!string.IsNullOrEmpty(context.PreferredVersion))
        {
            filtered = filtered.Where(i => i.Version == context.PreferredVersion);
        }

        if (context.PreferredTags is { Count: > 0 })
        {
            filtered = filtered.Where(i => context.PreferredTags.Overlaps(i.Tags));
        }

        return filtered.ToList();
    }
}

/// <summary>
/// 随机负载均衡器
/// </summary>
public sealed class RandomLoadBalancer : ILoadBalancer
{
    private static readonly Random Random = new();

    public string Name => "Random";

    public ServiceInstance? Select(IReadOnlyList<ServiceInstance> instances, LoadBalancerContext? context = null)
    {
        if (instances.Count == 0) return null;

        var filtered = FilterInstances(instances, context);
        if (filtered.Count == 0) return null;

        return filtered[Random.Next(filtered.Count)];
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
/// 加权随机负载均衡器
/// </summary>
public sealed class WeightedRandomLoadBalancer : ILoadBalancer
{
    private static readonly Random Random = new();

    public string Name => "WeightedRandom";

    public ServiceInstance? Select(IReadOnlyList<ServiceInstance> instances, LoadBalancerContext? context = null)
    {
        if (instances.Count == 0) return null;

        var filtered = FilterInstances(instances, context);
        if (filtered.Count == 0) return null;

        var totalWeight = filtered.Sum(i => i.Weight);
        if (totalWeight == 0) return filtered[0];

        var randomWeight = Random.Next(totalWeight);
        var cumulative = 0;

        foreach (var instance in filtered)
        {
            cumulative += instance.Weight;
            if (randomWeight < cumulative)
            {
                return instance;
            }
        }

        return filtered[^1];
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
/// 一致性哈希负载均衡器
/// </summary>
public sealed class ConsistentHashLoadBalancer : ILoadBalancer
{
    public string Name => "ConsistentHash";

    public ServiceInstance? Select(IReadOnlyList<ServiceInstance> instances, LoadBalancerContext? context = null)
    {
        if (instances.Count == 0) return null;

        var filtered = FilterInstances(instances, context);
        if (filtered.Count == 0) return null;

        var hashKey = context?.HashKey ?? context?.ClientId ?? Guid.NewGuid().ToString();
        var hash = GetStableHash(hashKey);
        var index = (int)(hash % (uint)filtered.Count);

        return filtered[index];
    }

    private static uint GetStableHash(string key)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var c in key)
            {
                hash = (hash ^ c) * 16777619;
            }
            return hash;
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
