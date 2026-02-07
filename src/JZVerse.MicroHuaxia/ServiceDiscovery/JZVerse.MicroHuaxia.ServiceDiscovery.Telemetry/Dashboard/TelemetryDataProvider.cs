using JZVerse.MicroHuaxia.ServiceDiscovery.Telemetry.Dashboard.Models;
using JZVerse.MicroHuaxia.ServiceDiscovery.Telemetry.Logging;
using JZVerse.MicroHuaxia.ServiceDiscovery.Telemetry.Metrics;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Telemetry.Dashboard;

/// <summary>
/// Dashboard 遥测数据提供者实现
/// </summary>
public sealed class TelemetryDataProvider(InMemoryLogStore _logStore, ServiceDiscoveryMetrics? _metrics = null)
    : ITelemetryDataProvider
{
    // 用于获取实时指标的回调
    private Func<int>? _getTotalServices;
    private Func<int>? _getTotalInstances;
    private Func<int>? _getHealthyInstances;
    private Func<int>? _getUnhealthyInstances;

    /// <summary>
    /// 设置实例统计数据源
    /// </summary>
    public void SetInstanceStatsSource(
        Func<int> getTotalServices,
        Func<int> getTotalInstances,
        Func<int> getHealthyInstances,
        Func<int> getUnhealthyInstances
    )
    {
        _getTotalServices = getTotalServices;
        _getTotalInstances = getTotalInstances;
        _getHealthyInstances = getHealthyInstances;
        _getUnhealthyInstances = getUnhealthyInstances;
    }

    public LogQueryResult QueryLogs(LogQuery query) => _logStore.Query(query);

    public LogStatistics GetLogStatistics() => _logStore.GetStatistics();

    public IReadOnlyList<LogEntry> GetLatestLogs(int count = 100) => _logStore.GetLatest(count);

    public Task<TraceListResult> GetTracesAsync(TraceQuery query, CancellationToken cancellationToken = default)
    {
        // 从日志中提取追踪信息 (简化实现)
        // 在实际场景中，可能需要单独的追踪存储
        var logs = _logStore.Query(
            new()
            {
                StartTime = query.StartTime,
                EndTime = query.EndTime,
                ServiceName = query.ServiceName,
                Take = 10000,
            }
        );

        var traces = logs
            .Logs.Where(l => !string.IsNullOrEmpty(l.TraceId))
            .GroupBy(l => l.TraceId!)
            .Select(g => new TraceSummary
            {
                TraceId = g.Key,
                StartTime = g.Min(l => l.Timestamp),
                DurationMs = (g.Max(l => l.Timestamp) - g.Min(l => l.Timestamp)).TotalMilliseconds,
                RootOperation = g.FirstOrDefault()?.Category ?? "Unknown",
                SpanCount = g.Select(l => l.SpanId).Distinct().Count(),
                HasErrors = g.Any(l => l.Level >= Microsoft.Extensions.Logging.LogLevel.Error),
                Services = g.Where(l => !string.IsNullOrEmpty(l.ServiceName))
                    .Select(l => l.ServiceName!)
                    .Distinct()
                    .ToList(),
            })
            .Where(t => !query.HasErrors.HasValue || t.HasErrors == query.HasErrors.Value)
            .Where(t => !query.MinDurationMs.HasValue || t.DurationMs >= query.MinDurationMs.Value)
            .OrderByDescending(t => t.StartTime)
            .Skip(query.Skip)
            .Take(query.Take)
            .ToList();

        return Task.FromResult(new TraceListResult { Traces = traces, TotalCount = traces.Count });
    }

    public Task<TraceDetail?> GetTraceDetailAsync(string traceId, CancellationToken cancellationToken = default)
    {
        var logs = _logStore.Query(new() { TraceId = traceId, Take = 1000 });

        if (logs.Logs.Count == 0)
        {
            return Task.FromResult<TraceDetail?>(null);
        }

        var spans = logs
            .Logs.Where(l => !string.IsNullOrEmpty(l.SpanId))
            .GroupBy(l => l.SpanId!)
            .Select(g => new SpanDetail
            {
                SpanId = g.Key,
                Name = g.FirstOrDefault()?.Category ?? "Unknown",
                Kind = "Internal",
                StartTime = g.Min(l => l.Timestamp),
                DurationMs = (g.Max(l => l.Timestamp) - g.Min(l => l.Timestamp)).TotalMilliseconds,
                Status = g.Any(l => l.Level >= Microsoft.Extensions.Logging.LogLevel.Error) ? "Error" : "Ok",
                Attributes = g.FirstOrDefault()?.Properties ?? new Dictionary<string, object?>(),
                Events = g.Select(l => new SpanEvent
                    {
                        Name = l.Message,
                        Timestamp = l.Timestamp,
                        Attributes = l.Properties,
                    })
                    .ToList(),
            })
            .OrderBy(s => s.StartTime)
            .ToList();

        var detail = new TraceDetail
        {
            TraceId = traceId,
            StartTime = spans.Min(s => s.StartTime),
            TotalDurationMs =
                spans.Count > 0
                    ? (
                        spans.Max(s => s.StartTime.AddMilliseconds(s.DurationMs)) - spans.Min(s => s.StartTime)
                    ).TotalMilliseconds
                    : 0,
            Spans = spans,
        };

        return Task.FromResult<TraceDetail?>(detail);
    }

    public Task<MetricSnapshot> GetMetricsSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = new MetricSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            TotalServices = _getTotalServices?.Invoke() ?? 0,
            TotalInstances = _getTotalInstances?.Invoke() ?? 0,
            HealthyInstances = _getHealthyInstances?.Invoke() ?? 0,
            UnhealthyInstances = _getUnhealthyInstances?.Invoke() ?? 0,
        };

        return Task.FromResult(snapshot);
    }

    public Task<MetricTimeSeries> GetMetricTimeSeriesAsync(
        string metricName,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default
    ) =>
        // 简化实现：返回空时间序列
        // 在实际场景中，需要从指标存储中查询历史数据
        Task.FromResult(new MetricTimeSeries { MetricName = metricName, DataPoints = [] });
}
