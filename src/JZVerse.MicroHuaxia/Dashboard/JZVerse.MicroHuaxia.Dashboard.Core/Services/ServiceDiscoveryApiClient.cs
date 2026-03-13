using System.Net.Http.Json;
using JZVerse.MicroHuaxia.Dashboard.Abstractions.Configuration;
using JZVerse.MicroHuaxia.Dashboard.Abstractions.Models;
using JZVerse.MicroHuaxia.Dashboard.Abstractions.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Dashboard.Core.Services;

/// <summary>
/// 服务发现 API 客户端实现
/// </summary>
public class ServiceDiscoveryApiClient : IServiceDiscoveryApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ServiceDiscoveryApiClient> _logger;

    public ServiceDiscoveryApiClient(
        HttpClient httpClient,
        IOptions<DashboardOptions> options,
        ILogger<ServiceDiscoveryApiClient> logger)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(options.Value.ServiceEndpoints.ServiceDiscovery);
        _logger = logger;
    }

    public async Task<List<ServiceInfo>> GetServicesAsync(CancellationToken cancellationToken = default)
    {
        return await GetServicesAsync(false, cancellationToken);
    }

    public async Task<List<ServiceInfo>> GetServicesAsync(bool includeDeregistered, CancellationToken cancellationToken = default)
    {
        try
        {
            var namesResponse = await _httpClient.GetAsync("/api/v1/services", cancellationToken);
            namesResponse.EnsureSuccessStatusCode();

            var serviceNames = await namesResponse.Content.ReadFromJsonAsync<List<string>>(cancellationToken);
            if (serviceNames == null || serviceNames.Count == 0)
            {
                return new List<ServiceInfo>();
            }

            var services = new List<ServiceInfo>();
            foreach (var serviceName in serviceNames)
            {
                try
                {
                    var url = includeDeregistered
                        ? $"/api/v1/services/{Uri.EscapeDataString(serviceName)}?includeDeregistered=true"
                        : $"/api/v1/services/{Uri.EscapeDataString(serviceName)}";
                    var instancesResponse = await _httpClient.GetAsync(url, cancellationToken);
                    instancesResponse.EnsureSuccessStatusCode();

                    var instances = await instancesResponse.Content.ReadFromJsonAsync<List<InstanceDto>>(cancellationToken);

                    var deregisteredCount = instances?.Count(i => i.DeregisteredAt.HasValue) ?? 0;
                    var activeInstances = instances?.Where(i => !i.DeregisteredAt.HasValue).ToList() ?? [];

                    services.Add(new ServiceInfo
                    {
                        ServiceName = serviceName,
                        InstanceCount = instances?.Count ?? 0,
                        HealthyCount = activeInstances.Count(i => i.Health == HealthStatus.Healthy),
                        UnhealthyCount = activeInstances.Count(i => i.Health != HealthStatus.Healthy),
                        DeregisteredCount = deregisteredCount,
                        Status = DetermineServiceStatus(instances)
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "获取服务 {ServiceName} 的实例信息失败，跳过", serviceName);
                    services.Add(new ServiceInfo
                    {
                        ServiceName = serviceName,
                        InstanceCount = 0,
                        HealthyCount = 0,
                        UnhealthyCount = 0,
                        DeregisteredCount = 0,
                        Status = ServiceStatus.Unknown
                    });
                }
            }

            return services;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取服务列表失败");
            return new List<ServiceInfo>();
        }
    }

    public async Task<List<ServiceInstance>> GetInstancesAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        return await GetInstancesAsync(serviceName, false, cancellationToken);
    }

    public async Task<List<ServiceInstance>> GetInstancesAsync(string serviceName, bool includeDeregistered, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = includeDeregistered
                ? $"/api/v1/services/{Uri.EscapeDataString(serviceName)}?includeDeregistered=true"
                : $"/api/v1/services/{Uri.EscapeDataString(serviceName)}";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var instances = await response.Content.ReadFromJsonAsync<List<InstanceDto>>(cancellationToken);
            if (instances == null)
            {
                return new List<ServiceInstance>();
            }

            return instances.Select(i => new ServiceInstance
            {
                InstanceId = i.InstanceId,
                ServiceName = i.ServiceName,
                Host = i.Host,
                Port = i.Port ?? 0,
                HealthStatus = i.Health,
                Weight = i.Weight,
                Metadata = i.Metadata?.Properties ?? new Dictionary<string, string>(),
                RegisterTime = i.RegisteredAt.DateTime,
                LastHeartbeat = i.LastHeartbeatAt.DateTime,
                DeregisteredAt = i.DeregisteredAt?.DateTime,
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取服务 {ServiceName} 的实例列表失败", serviceName);
            return new List<ServiceInstance>();
        }
    }

    public async Task<ServiceDiscoveryStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var services = await GetServicesAsync(cancellationToken);
        
        return new ServiceDiscoveryStats
        {
            TotalServices = services.Count,
            TotalInstances = services.Sum(s => s.InstanceCount),
            HealthyInstances = services.Sum(s => s.HealthyCount),
            UnhealthyInstances = services.Sum(s => s.UnhealthyCount)
        };
    }

    public async Task DeregisterInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/v1/services/{Uri.EscapeDataString(instanceId)}", cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "注销实例 {InstanceId} 失败", instanceId);
            throw;
        }
    }

    public async Task UpdateHealthStatusAsync(string instanceId, HealthStatus status, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"/api/v1/services/{Uri.EscapeDataString(instanceId)}/health",
                new { Status = status.ToString() },
                cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新实例 {InstanceId} 健康状态失败", instanceId);
            throw;
        }
    }

    public async Task UpdateMetadataAsync(string instanceId, Dictionary<string, string> metadata, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"/api/v1/services/{Uri.EscapeDataString(instanceId)}/metadata",
                new { Metadata = metadata },
                cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新实例 {InstanceId} 元数据失败", instanceId);
            throw;
        }
    }

    public async Task PurgeInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(
                $"/api/v1/services/{Uri.EscapeDataString(instanceId)}/purge", cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "永久删除实例 {InstanceId} 失败", instanceId);
            throw;
        }
    }

    private static ServiceStatus DetermineServiceStatus(List<InstanceDto>? instances)
    {
        if (instances == null || instances.Count == 0)
        {
            return ServiceStatus.Unknown;
        }

        // 区分活跃实例和已注销实例
        var activeInstances = instances.Where(i => !i.DeregisteredAt.HasValue).ToList();

        // 所有实例都已注销 → 已下线
        if (activeInstances.Count == 0)
        {
            return ServiceStatus.Offline;
        }

        var healthyCount = activeInstances.Count(i => i.Health == HealthStatus.Healthy);

        // 所有活跃实例健康
        if (healthyCount == activeInstances.Count)
        {
            return ServiceStatus.Healthy;
        }

        // 部分活跃实例健康
        if (healthyCount > 0)
        {
            return ServiceStatus.PartialHealthy;
        }

        // 所有活跃实例不健康 → 故障
        return ServiceStatus.Fault;
    }

    // 内部 DTO 类型，匹配服务端 ServiceInstance/ServiceMetadata 的 JSON 结构
    private class InstanceDto
    {
        public string InstanceId { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int? Port { get; set; }
        public HealthStatus Health { get; set; }
        public int Weight { get; set; } = 100;
        public MetadataDto? Metadata { get; set; }
        public DateTimeOffset RegisteredAt { get; set; }
        public DateTimeOffset LastHeartbeatAt { get; set; }
        public DateTimeOffset? DeregisteredAt { get; set; }
    }

    private class MetadataDto
    {
        public Dictionary<string, string>? Properties { get; set; }
        public List<string>? SupportedProtocols { get; set; }
        public string? Description { get; set; }
        public string? Owner { get; set; }
        public string? Environment { get; set; }
        public string? Region { get; set; }
        public string? HealthCheckEndpoint { get; set; }
        public string? ManagementEndpoint { get; set; }
    }
}
