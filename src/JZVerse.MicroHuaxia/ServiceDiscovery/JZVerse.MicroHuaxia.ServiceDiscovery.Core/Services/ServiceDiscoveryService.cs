using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Core.Services;

/// <summary>
/// 服务发现实现
/// </summary>
public class ServiceDiscovery(
    IServiceInstanceRepository _repository,
    IServiceDiscoveryCache _cache,
    ILoadBalancer _loadBalancer,
    ILogger<ServiceDiscovery> _logger
) : IServiceDiscovery
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ServiceInstance>> DiscoverAsync(
        ServiceQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var instances = await _repository.QueryAsync(query, cancellationToken);

        _logger.LogDebug("Discovered {Count} instances matching query", instances.Count);

        return instances;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ServiceInstance>> GetInstancesAsync(
        string serviceName,
        CancellationToken cancellationToken = default
    )
    {
        // 先从缓存获取
        if (_cache.IsCacheEnabled)
        {
            var cached = await _cache.GetCachedInstancesAsync(serviceName, cancellationToken);
            if (cached != null)
            {
                _logger.LogDebug("Retrieved {Count} instances for {ServiceName} from cache", cached.Count, serviceName);
                return cached;
            }
        }

        // 从仓储查询
        var query = new ServiceQuery
        {
            ServiceName = serviceName,
            OnlyHealthy = true,
            OnlyEnabled = true,
        };

        var instances = await _repository.QueryAsync(query, cancellationToken);

        // 更新缓存
        if (_cache.IsCacheEnabled && instances.Count > 0)
        {
            await _cache.SetCachedInstancesAsync(serviceName, instances, cancellationToken);
        }

        _logger.LogDebug("Retrieved {Count} instances for {ServiceName} from repository", instances.Count, serviceName);

        return instances;
    }

    /// <inheritdoc />
    public async Task<ServiceInstance?> GetInstanceAsync(
        string serviceName,
        CancellationToken cancellationToken = default
    )
    {
        var instances = await GetInstancesAsync(serviceName, cancellationToken);

        if (instances.Count == 0)
        {
            _logger.LogWarning("No available instances found for service: {ServiceName}", serviceName);
            return null;
        }

        // 使用负载均衡策略选择实例
        var selected = _loadBalancer.Select(instances);

        _logger.LogDebug(
            "Selected instance {InstanceId} for service {ServiceName} using {LoadBalancer}",
            selected?.InstanceId,
            serviceName,
            _loadBalancer.Name
        );

        return selected;
    }

    /// <inheritdoc />
    public Task<ServiceInstance?> GetInstanceByIdAsync(
        string instanceId,
        CancellationToken cancellationToken = default
    ) => _repository.GetByIdAsync(instanceId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetServiceNamesAsync(CancellationToken cancellationToken = default) =>
        _repository.GetServiceNamesAsync(cancellationToken);
}
