using JZVerse.MicroHuaxia.Gateway.Logging.Storage;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Gateway.Logging.Api;

/// <summary>
/// 日志查询 API 提供者实现
/// </summary>
public sealed class GatewayLogsProvider : IGatewayLogsProvider
{
    private readonly ILogStore _logStore;
    private readonly ILogger<GatewayLogsProvider> _logger;

    /// <summary>
    /// 创建日志查询提供者
    /// </summary>
    public GatewayLogsProvider(
        ILogStore logStore,
        ILogger<GatewayLogsProvider> logger)
    {
        _logStore = logStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<LogQueryResult> QueryAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        return _logStore.QueryAsync(query, cancellationToken);
    }

    /// <inheritdoc />
    public Task<LogEntry?> GetByIdAsync(string logId, CancellationToken cancellationToken = default)
    {
        return _logStore.GetByIdAsync(logId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<LogEntry>> GetByTraceIdAsync(string traceId, CancellationToken cancellationToken = default)
    {
        return _logStore.GetByTraceIdAsync(traceId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<LogEntry>> GetLatestAsync(int count, CancellationToken cancellationToken = default)
    {
        return _logStore.GetLatestAsync(count, cancellationToken);
    }

    /// <inheritdoc />
    public Task<LogStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        return _logStore.GetStatisticsAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetServiceNamesAsync(CancellationToken cancellationToken = default)
    {
        // 获取最近的日志统计服务名称
        var stats = await _logStore.GetStatisticsAsync(cancellationToken);
        return stats.CountByService.Keys.ToList();
    }

    /// <inheritdoc />
    public Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("执行日志清理");
        return _logStore.CleanupAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("清空所有日志");
        return _logStore.ClearAsync(cancellationToken);
    }
}
