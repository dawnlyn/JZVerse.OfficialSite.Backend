using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Core.Repositories;

/// <summary>
/// 基于内存的服务实例仓储（单节点版本）
/// </summary>
public class InMemoryServiceInstanceRepository : IServiceInstanceRepository
{
    private readonly ConcurrentDictionary<string, ServiceInstance> _instances = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _serviceNameIndex = new();
    private readonly Lock _indexLock = new();

    /// <inheritdoc />
    public Task<ServiceInstance> AddAsync(ServiceInstance instance, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(instance.InstanceId))
        {
            throw new ArgumentException("实例 ID 不能为空", nameof(instance));
        }

        if (!_instances.TryAdd(instance.InstanceId, instance))
        {
            throw new InvalidOperationException($"ID 为 '{instance.InstanceId}' 的实例已经存在。");
        }

        // 更新索引
        UpdateServiceIndex(instance.ServiceName, instance.InstanceId, true);

        return Task.FromResult(instance);
    }

    /// <inheritdoc />
    public Task<bool> UpdateAsync(ServiceInstance instance, CancellationToken cancellationToken = default)
    {
        if (!_instances.TryGetValue(instance.InstanceId, out var existingInstance))
        {
            return Task.FromResult(false);
        }

        // 如果服务名称变化，需要更新索引
        if (existingInstance.ServiceName != instance.ServiceName)
        {
            UpdateServiceIndex(existingInstance.ServiceName, instance.InstanceId, false);
            UpdateServiceIndex(instance.ServiceName, instance.InstanceId, true);
        }

        _instances[instance.InstanceId] = instance;
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<bool> RemoveAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        if (!_instances.TryRemove(instanceId, out var instance))
        {
            return Task.FromResult(false);
        }

        // 更新索引
        UpdateServiceIndex(instance.ServiceName, instanceId, false);

        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<ServiceInstance?> GetByIdAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        _instances.TryGetValue(instanceId, out var instance);
        return Task.FromResult(instance);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ServiceInstance>> GetByServiceNameAsync(
        string serviceName,
        CancellationToken cancellationToken = default
    )
    {
        if (!_serviceNameIndex.TryGetValue(serviceName, out var instanceIds))
        {
            return Task.FromResult<IReadOnlyList<ServiceInstance>>([]);
        }

        var instances = instanceIds
            .Keys.Select(id => _instances.GetValueOrDefault(id))
            .Where(inst => inst != null)
            .Cast<ServiceInstance>()
            .ToList();

        return Task.FromResult<IReadOnlyList<ServiceInstance>>(instances);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ServiceInstance>> QueryAsync(
        ServiceQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var results = _instances.Values.AsEnumerable();

        // 服务名称过滤
        if (!string.IsNullOrEmpty(query.ServiceName))
        {
            results = results.Where(i => MatchServiceName(i.ServiceName, query.ServiceName));
        }

        // 版本过滤
        if (!string.IsNullOrEmpty(query.Version))
        {
            results = results.Where(i => i.Version == query.Version);
        }

        // 标签过滤（AND 关系）
        if (query.Tags is { Count: > 0 })
        {
            results = results.Where(i => query.Tags.All(tag => i.Tags.Contains(tag)));
        }

        // 健康状态过滤
        if (query.HealthStatus.HasValue)
        {
            results = results.Where(i => i.Health == query.HealthStatus.Value);
        }

        // 只返回健康的实例
        if (query.OnlyHealthy)
        {
            results = results.Where(i => i.Health == HealthStatus.Healthy);
        }

        // 环境过滤
        if (!string.IsNullOrEmpty(query.Environment))
        {
            results = results.Where(i => i.Metadata.Environment == query.Environment);
        }

        // 区域过滤
        if (!string.IsNullOrEmpty(query.Region))
        {
            results = results.Where(i => i.Metadata.Region == query.Region);
        }

        // 启用状态过滤
        if (query.OnlyEnabled)
        {
            results = results.Where(i => i.Enabled);
        }

        // 已注销实例过滤
        if (!query.IncludeDeregistered)
        {
            results = results.Where(i => !i.IsDeregistered);
        }

        return Task.FromResult<IReadOnlyList<ServiceInstance>>(results.ToList());
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ServiceInstance>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ServiceInstance>>(_instances.Values.ToList());

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetServiceNamesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(_serviceNameIndex.Keys.ToList());

    private void UpdateServiceIndex(string serviceName, string instanceId, bool add)
    {
        lock (_indexLock)
        {
            if (add)
            {
                var instances = _serviceNameIndex.GetOrAdd(serviceName, _ => new());
                instances.TryAdd(instanceId, 0);
            }
            else
            {
                if (_serviceNameIndex.TryGetValue(serviceName, out var instances))
                {
                    instances.TryRemove(instanceId, out _);
                    if (instances.IsEmpty)
                    {
                        _serviceNameIndex.TryRemove(serviceName, out _);
                    }
                }
            }
        }
    }

    private static bool MatchServiceName(string serviceName, string pattern)
    {
        if (pattern == "*")
            return true;
        if (!pattern.Contains('*'))
            return serviceName == pattern;

        var regexPattern = $"^{Regex.Escape(pattern).Replace("\\*", ".*")}$";
        return Regex.IsMatch(serviceName, regexPattern);
    }
}
