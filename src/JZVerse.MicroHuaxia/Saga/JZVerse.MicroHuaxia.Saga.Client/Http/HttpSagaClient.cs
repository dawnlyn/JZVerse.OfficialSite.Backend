using System.Net.Http.Json;
using System.Text.Json;
using JZVerse.MicroHuaxia.Saga.Abstractions.Models;
using JZVerse.MicroHuaxia.Saga.Client.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Saga.Client.Http;

/// <summary>
/// HTTP Saga 客户端
/// </summary>
public class HttpSagaClient : ISagaClient
{
    private readonly HttpClient _httpClient;
    private readonly SagaClientOptions _options;
    private readonly ILogger<HttpSagaClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    private int _currentServerIndex;

    public HttpSagaClient(
        HttpClient httpClient,
        IOptions<SagaClientOptions> options,
        ILogger<HttpSagaClient> logger)
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
    public async Task<SagaInstance> StartSagaAsync(
        string sagaId,
        Dictionary<string, object>? initialData = null,
        CancellationToken cancellationToken = default) =>
        await ExecuteWithFailoverAsync(
            async baseUrl =>
            {
                var request = new { SagaId = sagaId, InitialData = initialData };
                var response = await _httpClient.PostAsJsonAsync(
                    $"{baseUrl}/api/v1/saga/instances", request, _jsonOptions, cancellationToken);
                response.EnsureSuccessStatusCode();
                var instance = await response.Content.ReadFromJsonAsync<SagaInstance>(_jsonOptions, cancellationToken);
                return instance ?? throw new InvalidOperationException("Failed to deserialize response");
            },
            cancellationToken);

    /// <inheritdoc />
    public async Task<SagaInstance?> GetInstanceAsync(string instanceId, CancellationToken cancellationToken = default) =>
        await ExecuteWithFailoverAsync(
            async baseUrl =>
            {
                var response = await _httpClient.GetAsync(
                    $"{baseUrl}/api/v1/saga/instances/{instanceId}", cancellationToken);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return null;
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<SagaInstance>(_jsonOptions, cancellationToken);
            },
            cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<SagaInstance>> QueryInstancesAsync(
        string? sagaId = null,
        SagaStatus? status = null,
        int limit = 100,
        CancellationToken cancellationToken = default) =>
        await ExecuteWithFailoverAsync(
            async baseUrl =>
            {
                var parameters = new List<string> { $"limit={limit}" };
                if (!string.IsNullOrEmpty(sagaId)) parameters.Add($"sagaId={sagaId}");
                if (status.HasValue) parameters.Add($"status={status.Value}");
                var queryString = string.Join("&", parameters);

                var response = await _httpClient.GetAsync(
                    $"{baseUrl}/api/v1/saga/instances?{queryString}", cancellationToken);
                response.EnsureSuccessStatusCode();
                var instances = await response.Content.ReadFromJsonAsync<List<SagaInstance>>(_jsonOptions, cancellationToken);
                return (IReadOnlyList<SagaInstance>)(instances ?? []);
            },
            cancellationToken);

    /// <inheritdoc />
    public async Task CompensateAsync(string instanceId, CancellationToken cancellationToken = default) =>
        await ExecuteWithFailoverAsync(
            async baseUrl =>
            {
                var response = await _httpClient.PostAsync(
                    $"{baseUrl}/api/v1/saga/instances/{instanceId}/compensate", null, cancellationToken);
                response.EnsureSuccessStatusCode();
                return true;
            },
            cancellationToken);

    /// <inheritdoc />
    public async Task<bool> CancelAsync(string instanceId, CancellationToken cancellationToken = default) =>
        await ExecuteWithFailoverAsync(
            async baseUrl =>
            {
                var response = await _httpClient.PostAsync(
                    $"{baseUrl}/api/v1/saga/instances/{instanceId}/cancel", null, cancellationToken);
                return response.IsSuccessStatusCode;
            },
            cancellationToken);

    /// <inheritdoc />
    public async Task<bool> ResumeAsync(string instanceId, CancellationToken cancellationToken = default) =>
        await ExecuteWithFailoverAsync(
            async baseUrl =>
            {
                var response = await _httpClient.PostAsync(
                    $"{baseUrl}/api/v1/saga/instances/{instanceId}/resume", null, cancellationToken);
                return response.IsSuccessStatusCode;
            },
            cancellationToken);

    /// <inheritdoc />
    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default) =>
        await ExecuteWithFailoverAsync(
            async baseUrl =>
            {
                var response = await _httpClient.GetAsync(
                    $"{baseUrl}/api/v1/saga/health", cancellationToken);
                return response.IsSuccessStatusCode;
            },
            cancellationToken);

    // ==================== 故障转移 ====================

    private async Task<T> ExecuteWithFailoverAsync<T>(Func<string, Task<T>> action, CancellationToken cancellationToken)
    {
        var servers = _options.ServerUrls;
        if (servers.Count == 0)
        {
            throw new InvalidOperationException("No saga server URLs configured");
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
                _logger.LogWarning(ex, "Failed to connect to saga server at {ServerUrl}", serverUrl);
                lastException = ex;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Request timeout to saga server at {ServerUrl}", serverUrl);
                lastException = new TimeoutException($"Request timeout to {serverUrl}");
            }
        }

        throw new InvalidOperationException("Failed to connect to any saga server", lastException);
    }
}
