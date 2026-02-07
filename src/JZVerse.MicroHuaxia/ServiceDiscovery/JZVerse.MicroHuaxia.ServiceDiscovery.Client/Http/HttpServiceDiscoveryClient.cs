using System.Net.Http.Json;
using System.Text.Json;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;
using JZVerse.MicroHuaxia.ServiceDiscovery.Client.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Client.Http;

/// <summary>
/// HTTP 服务发现客户端
/// </summary>
public class HttpServiceDiscoveryClient : IServiceDiscoveryClient
{
    private readonly HttpClient _httpClient;
    private readonly ServiceDiscoveryClientOptions _options;
    private readonly ILogger<HttpServiceDiscoveryClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    private int _currentServerIndex;

    public HttpServiceDiscoveryClient(
        HttpClient httpClient,
        IOptions<ServiceDiscoveryClientOptions> options,
        ILogger<HttpServiceDiscoveryClient> logger
    )
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };

        if (_options.ServerUrls.Count > 0)
        {
            _httpClient.BaseAddress = new(_options.ServerUrls[0]);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceInstance> RegisterAsync(
        ServiceRegistration registration,
        CancellationToken cancellationToken = default
    ) =>
        await ExecuteWithFailoverAsync(
            async baseUrl =>
            {
                var response = await _httpClient.PostAsJsonAsync(
                    $"{baseUrl}/api/v1/services/register",
                    registration,
                    _jsonOptions,
                    cancellationToken
                );

                response.EnsureSuccessStatusCode();

                var instance = await response.Content.ReadFromJsonAsync<ServiceInstance>(
                    _jsonOptions,
                    cancellationToken
                );
                return instance ?? throw new InvalidOperationException("Failed to deserialize response");
            },
            cancellationToken
        );

    /// <inheritdoc />
    public async Task<bool> DeregisterAsync(string instanceId, CancellationToken cancellationToken = default) =>
        await ExecuteWithFailoverAsync(
            async baseUrl =>
            {
                var response = await _httpClient.DeleteAsync(
                    $"{baseUrl}/api/v1/services/{instanceId}",
                    cancellationToken
                );

                return response.IsSuccessStatusCode;
            },
            cancellationToken
        );

    /// <inheritdoc />
    public async Task<bool> HeartbeatAsync(string instanceId, CancellationToken cancellationToken = default) =>
        await ExecuteWithFailoverAsync(
            async baseUrl =>
            {
                var response = await _httpClient.PutAsync(
                    $"{baseUrl}/api/v1/services/{instanceId}/heartbeat",
                    null,
                    cancellationToken
                );

                return response.IsSuccessStatusCode;
            },
            cancellationToken
        );

    /// <inheritdoc />
    public async Task<IReadOnlyList<ServiceInstance>> GetInstancesAsync(
        string serviceName,
        CancellationToken cancellationToken = default
    ) =>
        await ExecuteWithFailoverAsync(
            async baseUrl =>
            {
                var response = await _httpClient.GetAsync(
                    $"{baseUrl}/api/v1/services/{serviceName}",
                    cancellationToken
                );

                response.EnsureSuccessStatusCode();

                var instances = await response.Content.ReadFromJsonAsync<List<ServiceInstance>>(
                    _jsonOptions,
                    cancellationToken
                );
                return instances ?? [];
            },
            cancellationToken
        );

    /// <inheritdoc />
    public async Task<ServiceInstance?> GetInstanceAsync(
        string serviceName,
        CancellationToken cancellationToken = default
    ) =>
        await ExecuteWithFailoverAsync(
            async baseUrl =>
            {
                var response = await _httpClient.GetAsync(
                    $"{baseUrl}/api/v1/services/{serviceName}/instance",
                    cancellationToken
                );

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<ServiceInstance>(_jsonOptions, cancellationToken);
            },
            cancellationToken
        );

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetServiceNamesAsync(CancellationToken cancellationToken = default) =>
        await ExecuteWithFailoverAsync(
            async baseUrl =>
            {
                var response = await _httpClient.GetAsync($"{baseUrl}/api/v1/services", cancellationToken);

                response.EnsureSuccessStatusCode();

                var names = await response.Content.ReadFromJsonAsync<List<string>>(_jsonOptions, cancellationToken);
                return names ?? [];
            },
            cancellationToken
        );

    /// <inheritdoc />
    public async Task<IReadOnlyList<ServiceInstance>> DiscoverAsync(
        ServiceQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await ExecuteWithFailoverAsync(
            async baseUrl =>
            {
                var response = await _httpClient.PostAsJsonAsync(
                    $"{baseUrl}/api/v1/services/discover",
                    query,
                    _jsonOptions,
                    cancellationToken
                );

                response.EnsureSuccessStatusCode();

                var instances = await response.Content.ReadFromJsonAsync<List<ServiceInstance>>(
                    _jsonOptions,
                    cancellationToken
                );
                return instances ?? [];
            },
            cancellationToken
        );

    private async Task<T> ExecuteWithFailoverAsync<T>(Func<string, Task<T>> action, CancellationToken cancellationToken)
    {
        var servers = _options.ServerUrls;
        if (servers.Count == 0)
        {
            throw new InvalidOperationException("No server URLs configured");
        }

        Exception? lastException = null;

        for (var i = 0; i < servers.Count; i++)
        {
            var serverIndex = (_currentServerIndex + i) % servers.Count;
            var serverUrl = servers[serverIndex].TrimEnd('/');

            try
            {
                var result = await action(serverUrl);
                _currentServerIndex = serverIndex;
                return result;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Failed to connect to service discovery server at {ServerUrl}", serverUrl);
                lastException = ex;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Request timeout to service discovery server at {ServerUrl}", serverUrl);
                lastException = new TimeoutException($"Request timeout to {serverUrl}");
            }
        }

        throw new InvalidOperationException("Failed to connect to any service discovery server", lastException);
    }
}
