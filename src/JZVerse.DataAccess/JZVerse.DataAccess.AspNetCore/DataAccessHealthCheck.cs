using JZVerse.DataAccess.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace JZVerse.DataAccess.AspNetCore;

/// <summary>
/// 数据库连接健康检查
/// </summary>
public sealed class DataAccessHealthCheck : IHealthCheck
{
    private readonly IDbConnectionFactory _connectionFactory;

    /// <summary>
    /// 创建数据库健康检查
    /// </summary>
    public DataAccessHealthCheck(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var isHealthy = await _connectionFactory.TestConnectionAsync(cancellationToken);

            if (isHealthy)
            {
                return HealthCheckResult.Healthy(
                    $"{_connectionFactory.DatabaseType} connection is healthy");
            }

            return HealthCheckResult.Unhealthy(
                $"{_connectionFactory.DatabaseType} connection test failed");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                $"{_connectionFactory.DatabaseType} connection error: {ex.Message}",
                ex);
        }
    }
}
