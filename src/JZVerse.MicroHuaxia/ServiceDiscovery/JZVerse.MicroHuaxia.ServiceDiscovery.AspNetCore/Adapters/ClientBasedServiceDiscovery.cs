using System.Net.Http.Json;
using System.Text.Json;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;
using JZVerse.MicroHuaxia.ServiceDiscovery.Client;
using JZVerse.MicroHuaxia.ServiceDiscovery.Client.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.AspNetCore.Adapters;

/// <summary>
/// 基于客户端的服务发现适配器
/// 将 IServiceDiscoveryClient 适配为 IServiceDiscovery 接口
/// 所有方法均容错处理：SD 不可用时返回空结果而非抛异常
/// </summary>
public class ClientBasedServiceDiscovery(
    IServiceDiscoveryClient _client,
    IHttpClientFactory _httpClientFactory,
    IOptions<ServiceDiscoveryClientOptions> _options,
    ILogger<ClientBasedServiceDiscovery> _logger
) : IServiceDiscovery
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <inheritdoc />
    public async Task<IReadOnlyList<ServiceInstance>> GetInstancesAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _client.GetInstancesAsync(serviceName, cancellationToken);
        }
        catch (Exception ex) when (IsTransientError(ex))
        {
            _logger.LogWarning(ex, "服务发现不可用，无法获取服务 {ServiceName} 的实例列表", serviceName);
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<ServiceInstance?> GetInstanceAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _client.GetInstanceAsync(serviceName, cancellationToken);
        }
        catch (Exception ex) when (IsTransientError(ex))
        {
            _logger.LogWarning(ex, "服务发现不可用，无法获取服务 {ServiceName} 的实例", serviceName);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceInstance?> GetInstanceByIdAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var serverUrl = GetServerUrl();
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            var response = await httpClient.GetAsync(
                $"{serverUrl}/api/v1/instances/{instanceId}",
                cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ServiceInstance>(_jsonOptions, cancellationToken);
        }
        catch (Exception ex) when (IsTransientError(ex))
        {
            _logger.LogWarning(ex, "服务发现不可用，无法获取实例 {InstanceId} 的详情", instanceId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetServiceNamesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _client.GetServiceNamesAsync(cancellationToken);
        }
        catch (Exception ex) when (IsTransientError(ex))
        {
            _logger.LogWarning(ex, "服务发现不可用，无法获取服务名称列表");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ServiceInstance>> DiscoverAsync(
        ServiceQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _client.DiscoverAsync(query, cancellationToken);
        }
        catch (Exception ex) when (IsTransientError(ex))
        {
            _logger.LogWarning(ex, "服务发现不可用，无法执行服务查询");
            return [];
        }
    }

    private string GetServerUrl()
    {
        var urls = _options.Value.ServerUrls;
        return urls.Count > 0 ? urls[0].TrimEnd('/') : "http://localhost:5100";
    }

    private static bool IsTransientError(Exception ex) =>
        ex is HttpRequestException or TimeoutException or TaskCanceledException;
}
