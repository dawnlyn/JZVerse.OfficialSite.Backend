using System.Diagnostics;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.HealthChecks;

/// <summary>
/// HTTP 健康检查器
/// </summary>
public class HttpHealthChecker(
    IHttpClientFactory _httpClientFactory,
    ILogger<HttpHealthChecker> _logger,
    int _timeoutSeconds = 5
) : IHealthChecker
{
    /// <inheritdoc />
    public string Name => "HTTP";

    /// <inheritdoc />
    public bool CanCheck(ServiceInstance instance) =>
        instance.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
        || instance.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        ServiceInstance instance,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var healthCheckUrl = BuildHealthCheckUrl(instance);
            var httpClient = _httpClientFactory.CreateClient("HealthCheck");
            httpClient.Timeout = TimeSpan.FromSeconds(_timeoutSeconds);

            _logger.LogDebug(
                "Performing HTTP health check for {ServiceName}:{InstanceId} at {Url}",
                instance.ServiceName,
                instance.InstanceId,
                healthCheckUrl
            );

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

            var response = await httpClient.GetAsync(healthCheckUrl, cts.Token);
            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                return new()
                {
                    Status = HealthStatus.Healthy,
                    Message = $"HTTP {(int)response.StatusCode} OK",
                    Duration = stopwatch.Elapsed,
                    Data = new() { ["StatusCode"] = (int)response.StatusCode, ["Url"] = healthCheckUrl },
                };
            }

            _logger.LogWarning(
                "HTTP health check failed for {ServiceName}:{InstanceId}: {StatusCode}",
                instance.ServiceName,
                instance.InstanceId,
                (int)response.StatusCode
            );

            return new()
            {
                Status = HealthStatus.Unhealthy,
                Message = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}",
                Duration = stopwatch.Elapsed,
                Data = new() { ["StatusCode"] = (int)response.StatusCode, ["Url"] = healthCheckUrl },
            };
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                ex,
                "HTTP health check failed for {ServiceName}:{InstanceId}",
                instance.ServiceName,
                instance.InstanceId
            );

            return new()
            {
                Status = HealthStatus.Unhealthy,
                Message = $"Connection error: {ex.Message}",
                Duration = stopwatch.Elapsed,
            };
        }
        catch (TaskCanceledException)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                "HTTP health check timeout for {ServiceName}:{InstanceId}",
                instance.ServiceName,
                instance.InstanceId
            );

            return new()
            {
                Status = HealthStatus.Unhealthy,
                Message = "Health check timeout",
                Duration = stopwatch.Elapsed,
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Unexpected error during HTTP health check for {ServiceName}:{InstanceId}",
                instance.ServiceName,
                instance.InstanceId
            );

            return new()
            {
                Status = HealthStatus.Unhealthy,
                Message = $"Unexpected error: {ex.Message}",
                Duration = stopwatch.Elapsed,
            };
        }
    }

    private static string BuildHealthCheckUrl(ServiceInstance instance)
    {
        var endpoint = instance.Metadata.HealthCheckEndpoint ?? "/health";
        if (!endpoint.StartsWith('/'))
        {
            endpoint = "/" + endpoint;
        }

        return $"{instance.Scheme}://{instance.Host}:{instance.Port}{endpoint}";
    }
}
