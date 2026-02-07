using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.LoadBalancing;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ServiceCommunication.Core.LoadBalancing;

/// <summary>
/// 自适应加权负载均衡器
/// 基于实例的响应时间和成功率动态调整权重
/// </summary>
public sealed class AdaptiveWeightedLoadBalancer : ILoadBalancer
{
    private readonly IInstanceMetricsCollector _metricsCollector;
    private readonly Lock _lock = new();
    private int _index;

    public AdaptiveWeightedLoadBalancer(IInstanceMetricsCollector metricsCollector)
    {
        _metricsCollector = metricsCollector;
    }

    public string Name => "AdaptiveWeighted";

    public ServiceInstance? Select(IReadOnlyList<ServiceInstance> instances, LoadBalancerContext? context = null)
    {
        if (instances.Count == 0)
        {
            return null;
        }

        if (instances.Count == 1)
        {
            return instances[0];
        }

        // 过滤实例
        var filtered = FilterInstances(instances, context);
        if (filtered.Count == 0)
        {
            return null;
        }

        if (filtered.Count == 1)
        {
            return filtered[0];
        }

        // 构建带权重的实例列表
        var weightedInstances = filtered
            .Select(instance =>
            {
                var metrics = _metricsCollector.GetMetrics(instance.InstanceId);
                var weight = metrics?.CalculatedWeight ?? instance.Weight;
                // 确保权重至少为 1
                return (Instance: instance, Weight: Math.Max(1, (int)weight));
            })
            .ToList();

        // 使用平滑加权轮询算法
        return SelectBySmoothedWeightedRoundRobin(weightedInstances);
    }

    private ServiceInstance SelectBySmoothedWeightedRoundRobin(
        List<(ServiceInstance Instance, int Weight)> weightedInstances)
    {
        lock (_lock)
        {
            var totalWeight = weightedInstances.Sum(x => x.Weight);

            // 构建加权列表（简化实现）
            var expandedList = new List<ServiceInstance>();
            foreach (var (instance, weight) in weightedInstances)
            {
                // 归一化权重，避免列表过大
                var normalizedWeight = Math.Max(1, weight / 10);
                for (var i = 0; i < normalizedWeight; i++)
                {
                    expandedList.Add(instance);
                }
            }

            if (expandedList.Count == 0)
            {
                return weightedInstances[0].Instance;
            }

            _index = (_index + 1) % expandedList.Count;
            return expandedList[_index];
        }
    }

    private static List<ServiceInstance> FilterInstances(
        IReadOnlyList<ServiceInstance> instances,
        LoadBalancerContext? context)
    {
        var filtered = instances.AsEnumerable();

        // 排除已失败的实例
        if (context?.ExcludedInstanceIds is { Count: > 0 })
        {
            filtered = filtered.Where(i => !context.ExcludedInstanceIds.Contains(i.InstanceId));
        }

        // 按版本过滤
        if (!string.IsNullOrEmpty(context?.PreferredVersion))
        {
            var versioned = filtered.Where(i => i.Version == context.PreferredVersion).ToList();
            if (versioned.Count > 0)
            {
                filtered = versioned;
            }
        }

        // 按标签过滤
        if (context?.PreferredTags is { Count: > 0 })
        {
            var tagged = filtered.Where(i =>
                i.Tags.Count > 0 && context.PreferredTags.Overlaps(i.Tags)).ToList();
            if (tagged.Count > 0)
            {
                filtered = tagged;
            }
        }

        return filtered.ToList();
    }

    /// <summary>
    /// 获取当前所有实例的权重（用于监控）
    /// </summary>
    public IReadOnlyDictionary<string, double> GetCurrentWeights(IReadOnlyList<ServiceInstance> instances)
    {
        return instances.ToDictionary(
            instance => instance.InstanceId,
            instance =>
            {
                var metrics = _metricsCollector.GetMetrics(instance.InstanceId);
                return metrics?.CalculatedWeight ?? instance.Weight;
            });
    }
}
