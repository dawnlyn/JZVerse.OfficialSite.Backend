using System.Net.Http.Json;
using System.Text.Json;
using JZVerse.MicroHuaxia.Gateway.Abstractions;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Authentication;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;
using JZVerse.MicroHuaxia.Gateway.Client.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Gateway.Client.Http;

/// <summary>
/// HTTP 网关客户端
/// </summary>
public class HttpGatewayClient : IGatewayClient
{
    private readonly HttpClient _httpClient;
    private readonly GatewayClientOptions _options;
    private readonly ILogger<HttpGatewayClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    private int _currentServerIndex;

    public HttpGatewayClient(
        HttpClient httpClient,
        IOptions<GatewayClientOptions> options,
        ILogger<HttpGatewayClient> logger)
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

    // ==================== 路由管理 ====================

    /// <inheritdoc />
    public async Task<IReadOnlyList<GatewayRoute>> GetRoutesAsync(CancellationToken cancellationToken = default) =>
        await ExecuteWithFailoverAsync(
            async baseUrl =>
            {
                var response = await _httpClient.GetAsync($"{baseUrl}/api/v1/gateway/routes", cancellationToken);
                response.EnsureSuccessStatusCode();
                var routes = await response.Content.ReadFromJsonAsync<List<GatewayRoute>>(_jsonOptions, cancellationToken);
                return (IReadOnlyList<GatewayRoute>)(routes ?? []);
            },
            cancellationToken);

    /// <inheritdoc />
    public async Task<GatewayRoute?> GetRouteAsync(string routeId, CancellationToken cancellationToken = default) =>
        await ExecuteWithFailoverAsync(
            async baseUrl =>
            {
                var response = await _httpClient.GetAsync($"{baseUrl}/api/v1/gateway/routes/{routeId}", cancellationToken);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return null;
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<GatewayRoute>(_jsonOptions, cancellationToken);
            },
            cancellationToken);

    /// <inheritdoc />
    public async Task<GatewayRoute> AddRouteAsync(GatewayRoute route, CancellationToken cancellationToken = default) =>
        await ExecuteWithFailoverAsync(
            async baseUrl =>
            {
                var response = await _httpClient.PostAsJsonAsync($"{baseUrl}/api/v1/gateway/routes", route, _jsonOptions, cancellationToken);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<GatewayRoute>(_jsonOptions, cancellationToken);
                return result ?? throw new InvalidOperationException("Failed to deserialize response");
            },
            cancellationToken);

    /// <inheritdoc />
    public async Task<GatewayRoute> UpdateRouteAsync(string routeId, GatewayRoute route, CancellationToken cancellationToken = default) =>
        await ExecuteWithFailoverAsync(
            async baseUrl =>
            {
                var response = await _httpClient.PutAsJsonAsync($"{baseUrl}/api/v1/gateway/routes/{routeId}", route, _jsonOptions, cancellationToken);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<GatewayRoute>(_jsonOptions, cancellationToken);
                return result ?? throw new InvalidOperationException("Failed to deserialize response");
            },
            cancellationToken);

    /// <inheritdoc />
    public async Task<bool> DeleteRouteAsync(string routeId, CancellationToken cancellationToken = default) =>
        await ExecuteWithFailoverAsync(
            async baseUrl =>
            {
                var response = await _httpClient.DeleteAsync($"{baseUrl}/api/v1/gateway/routes/{routeId}", cancellationToken);
                return response.IsSuccessStatusCode;
            },
            cancellationToken);

    /// <inheritdoc />
    public async Task<int> BatchUpdateRoutesAsync(IEnumerable<GatewayRoute> routes, CancellationToken cancellationToken = default) =>
        await ExecuteWithFailoverAsync(
            async baseUrl =>
            {
                var response = await _httpClient.PostAsJsonAsync($"{baseUrl}/api/v1/gateway/routes/batch", routes, _jsonOptions, cancellationToken);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions, cancellationToken);
                return result.GetProperty("count").GetInt32();
            },
            cancellationToken);

    // ==================== 认证策略管理 ====================

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuthenticationStrategy>> GetAuthStrategiesAsync(CancellationToken cancellationToken = default) =>
        await ExecuteWithFailoverAsync(
            async baseUrl =>
            {
                var response = await _httpClient.GetAsync($"{baseUrl}/api/v1/gateway/authentication/strategies", cancellationToken);
                response.EnsureSuccessStatusCode();
                var strategies = await response.Content.ReadFromJsonAsync<List<AuthenticationStrategy>>(_jsonOptions, cancellationToken);
                return (IReadOnlyList<AuthenticationStrategy>)(strategies ?? []);
            },
            cancellationToken);

    /// <inheritdoc />
    public async Task<AuthenticationStrategy?> GetAuthStrategyAsync(string name, CancellationToken cancellationToken = default) =>
        await ExecuteWithFailoverAsync(
            async baseUrl =>
            {
                var response = await _httpClient.GetAsync($"{baseUrl}/api/v1/gateway/authentication/strategies/{name}", cancellationToken);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return null;
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<AuthenticationStrategy>(_jsonOptions, cancellationToken);
            },
            cancellationToken);

    /// <inheritdoc />
    public async Task<AuthenticationStrategy> AddAuthStrategyAsync(AuthenticationStrategy strategy, CancellationToken cancellationToken = default) =>
        await ExecuteWithFailoverAsync(
            async baseUrl =>
            {
                var response = await _httpClient.PostAsJsonAsync($"{baseUrl}/api/v1/gateway/authentication/strategies", strategy, _jsonOptions, cancellationToken);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<AuthenticationStrategy>(_jsonOptions, cancellationToken);
                return result ?? throw new InvalidOperationException("Failed to deserialize response");
            },
            cancellationToken);

    /// <inheritdoc />
    public async Task<bool> DeleteAuthStrategyAsync(string name, CancellationToken cancellationToken = default) =>
        await ExecuteWithFailoverAsync(
            async baseUrl =>
            {
                var response = await _httpClient.DeleteAsync($"{baseUrl}/api/v1/gateway/authentication/strategies/{name}", cancellationToken);
                return response.IsSuccessStatusCode;
            },
            cancellationToken);

    // ==================== 审计日志 ====================

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditLogEntry>> QueryAuditLogsAsync(AuditLogQuery query, CancellationToken cancellationToken = default) =>
        await ExecuteWithFailoverAsync(
            async baseUrl =>
            {
                var response = await _httpClient.PostAsJsonAsync($"{baseUrl}/api/v1/gateway/audit/query", query, _jsonOptions, cancellationToken);
                response.EnsureSuccessStatusCode();
                var logs = await response.Content.ReadFromJsonAsync<List<AuditLogEntry>>(_jsonOptions, cancellationToken);
                return (IReadOnlyList<AuditLogEntry>)(logs ?? []);
            },
            cancellationToken);

    /// <inheritdoc />
    public async Task<AuditLogStatistics> GetAuditStatisticsAsync(DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken cancellationToken = default) =>
        await ExecuteWithFailoverAsync(
            async baseUrl =>
            {
                var queryString = "";
                var parameters = new List<string>();
                if (from.HasValue) parameters.Add($"from={from.Value:O}");
                if (to.HasValue) parameters.Add($"to={to.Value:O}");
                if (parameters.Count > 0) queryString = "?" + string.Join("&", parameters);

                var response = await _httpClient.GetAsync($"{baseUrl}/api/v1/gateway/audit/statistics{queryString}", cancellationToken);
                response.EnsureSuccessStatusCode();
                var stats = await response.Content.ReadFromJsonAsync<AuditLogStatistics>(_jsonOptions, cancellationToken);
                return stats ?? throw new InvalidOperationException("Failed to deserialize response");
            },
            cancellationToken);

    // ==================== 健康检查 ====================

    /// <inheritdoc />
    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default) =>
        await ExecuteWithFailoverAsync(
            async baseUrl =>
            {
                var response = await _httpClient.GetAsync($"{baseUrl}/api/v1/gateway/health", cancellationToken);
                return response.IsSuccessStatusCode;
            },
            cancellationToken);

    // ==================== 故障转移 ====================

    private async Task<T> ExecuteWithFailoverAsync<T>(Func<string, Task<T>> action, CancellationToken cancellationToken)
    {
        var servers = _options.ServerUrls;
        if (servers.Count == 0)
        {
            throw new InvalidOperationException("No gateway server URLs configured");
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
                _logger.LogWarning(ex, "Failed to connect to gateway server at {ServerUrl}", serverUrl);
                lastException = ex;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Request timeout to gateway server at {ServerUrl}", serverUrl);
                lastException = new TimeoutException($"Request timeout to {serverUrl}");
            }
        }

        throw new InvalidOperationException("Failed to connect to any gateway server", lastException);
    }
}
