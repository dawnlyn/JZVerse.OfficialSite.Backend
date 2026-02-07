using System.Diagnostics;
using System.Net.Sockets;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.HealthChecks;

/// <summary>
/// TCP 健康检查器（端口连通性检查）
/// </summary>
public class TcpHealthChecker(ILogger<TcpHealthChecker> _logger, int _timeoutSeconds = 5) : IHealthChecker
{
    /// <inheritdoc />
    public string Name => "TCP";

    /// <inheritdoc />
    public bool CanCheck(ServiceInstance instance)
    {
        // TCP 检查需要端口号
        if (instance.Port is not > 0)
            return false;

        // TCP 检查器可以检查 tcp 协议，或者明确指定了 tcp 健康检查类型
        return instance.Scheme.Equals("tcp", StringComparison.OrdinalIgnoreCase)
            || instance.Metadata.Properties.TryGetValue("HealthCheckType", out var type)
                && type.Equals("tcp", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        ServiceInstance instance,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();

        // 确保有端口号
        if (instance.Port is not > 0)
        {
            return new()
            {
                Status = HealthStatus.Unhealthy,
                Message = "TCP health check requires a valid port number",
                Duration = stopwatch.Elapsed,
            };
        }

        var port = instance.Port.Value;

        try
        {
            _logger.LogDebug(
                "Performing TCP health check for {ServiceName}:{InstanceId} at {Host}:{Port}",
                instance.ServiceName,
                instance.InstanceId,
                instance.Host,
                port
            );

            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

            await client.ConnectAsync(instance.Host, port, cts.Token);
            stopwatch.Stop();

            return new()
            {
                Status = HealthStatus.Healthy,
                Message = "TCP connection successful",
                Duration = stopwatch.Elapsed,
                Data = new() { ["Host"] = instance.Host, ["Port"] = port },
            };
        }
        catch (SocketException ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                ex,
                "TCP health check failed for {ServiceName}:{InstanceId}",
                instance.ServiceName,
                instance.InstanceId
            );

            return new()
            {
                Status = HealthStatus.Unhealthy,
                Message = $"TCP connection failed: {ex.Message}",
                Duration = stopwatch.Elapsed,
            };
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                "TCP health check timeout for {ServiceName}:{InstanceId}",
                instance.ServiceName,
                instance.InstanceId
            );

            return new()
            {
                Status = HealthStatus.Unhealthy,
                Message = "TCP connection timeout",
                Duration = stopwatch.Elapsed,
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Unexpected error during TCP health check for {ServiceName}:{InstanceId}",
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
}
