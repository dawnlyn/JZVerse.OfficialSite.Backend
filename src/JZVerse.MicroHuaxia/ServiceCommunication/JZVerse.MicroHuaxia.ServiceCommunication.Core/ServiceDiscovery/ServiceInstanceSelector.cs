using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.Configuration;
using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.LoadBalancing;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.ServiceCommunication.Core.ServiceDiscovery;

/// <summary>
/// 服务实例选择器实现 - 整合服务发现和负载均衡
/// </summary>
public sealed class ServiceInstanceSelector(
    IServiceDiscovery serviceDiscovery,
    ILoadBalancerFactory loadBalancerFactory,
    IOptions<ServiceCommunicationOptions> options,
    ILogger<ServiceInstanceSelector> logger) : IServiceInstanceSelector
{
    private readonly ServiceCommunicationOptions _options = options.Value;

    public async Task<ServiceInstance?> SelectAsync(
        string serviceName,
        LoadBalancerContext? context = null,
        CancellationToken cancellationToken = default)
    {
        // 1. 获取服务端点配置
        var serviceConfig = _options.ServiceEndpoints.GetValueOrDefault(serviceName);

        // 2. 如果配置了直连地址，使用直连模式
        if (serviceConfig?.Addresses is { Count: > 0 })
        {
            return CreateInstanceFromAddress(serviceName, serviceConfig.Addresses, context);
        }

        // 3. 从服务发现获取实例列表
        var instances = await GetInstancesAsync(serviceName, cancellationToken);
        if (instances.Count == 0)
        {
            logger.LogWarning("No instances found for service '{ServiceName}'", serviceName);
            return null;
        }

        // 4. 使用负载均衡器选择实例
        var strategy = serviceConfig?.LoadBalancer ?? _options.DefaultLoadBalancer;
        var loadBalancer = loadBalancerFactory.GetOrCreate(serviceName, strategy);

        var selected = loadBalancer.Select(instances, context);

        if (selected is not null)
        {
            logger.LogDebug(
                "Selected instance {InstanceId} ({Address}) for service '{ServiceName}' using {Strategy}",
                selected.InstanceId,
                selected.Address,
                serviceName,
                strategy);
        }

        return selected;
    }

    public async Task<IReadOnlyList<ServiceInstance>> GetInstancesAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var instances = await serviceDiscovery.GetInstancesAsync(serviceName, cancellationToken);

            // 只返回健康且启用的实例
            return instances
                .Where(i => i.Enabled && i.Health == HealthStatus.Healthy)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get instances for service '{ServiceName}'", serviceName);
            return [];
        }
    }

    public void ReportInstanceStatus(
        ServiceInstance instance,
        bool success,
        TimeSpan duration,
        Exception? exception = null)
    {
        if (success)
        {
            logger.LogDebug(
                "Request to {InstanceId} succeeded in {Duration}ms",
                instance.InstanceId,
                duration.TotalMilliseconds);
        }
        else
        {
            logger.LogWarning(
                exception,
                "Request to {InstanceId} failed after {Duration}ms",
                instance.InstanceId,
                duration.TotalMilliseconds);
        }

        // TODO: 可以在这里实现实例状态上报，用于自适应负载均衡
    }

    private ServiceInstance? CreateInstanceFromAddress(
        string serviceName,
        IReadOnlyList<string> addresses,
        LoadBalancerContext? context)
    {
        // 简单轮询直连地址
        var index = context?.HashKey?.GetHashCode() ?? 0;
        var address = addresses[Math.Abs(index) % addresses.Count];

        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri))
        {
            logger.LogWarning("Invalid address '{Address}' for service '{ServiceName}'", address, serviceName);
            return null;
        }

        return new ServiceInstance
        {
            InstanceId = $"direct-{serviceName}-{uri.Host}:{uri.Port}",
            ServiceName = serviceName,
            Host = uri.Host,
            Port = uri.Port,
            Scheme = uri.Scheme,
            Health = HealthStatus.Healthy,
            Enabled = true,
        };
    }
}
