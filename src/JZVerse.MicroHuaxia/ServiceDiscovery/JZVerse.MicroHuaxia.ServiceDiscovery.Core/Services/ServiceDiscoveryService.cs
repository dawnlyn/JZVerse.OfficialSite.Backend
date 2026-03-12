using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Core.Services;

/// <summary>
/// 服务发现实现
/// </summary>
public class ServiceDiscoveryService(
    IServiceInstanceRepository _repository,
    IServiceDiscoveryCache _cache,
    ILoadBalancer _loadBalancer,
    ILogger<ServiceDiscoveryService> _logger
) : IServiceDiscovery
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ServiceInstance>> DiscoverAsync(
        ServiceQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var instances = await _repository.QueryAsync(query, cancellationToken);

        _logger.LogDebug("发现 {Count} 个匹配查询的服务实例", instances.Count);

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
                _logger.LogDebug("从缓存中获取到 {Count} 个 {ServiceName} 的服务实例", cached.Count, serviceName);
                return cached;
            }
        }

        // 如果缓存没有，从仓储查询
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

        _logger.LogDebug("从仓储中获取到 {Count} 个 {ServiceName} 的服务实例", instances.Count, serviceName);

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
            _logger.LogWarning("未找到服务 {ServiceName} 的可用实例", serviceName);
            return null;
        }

        // 使用负载均衡策略选择实例
        var selected = _loadBalancer.Select(instances);

        _logger.LogDebug(
            "使用 {LoadBalancer} 负载均衡器为服务 {ServiceName} 选择了实例 {InstanceId}",
            _loadBalancer.Name,
            serviceName,
            selected?.InstanceId
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
