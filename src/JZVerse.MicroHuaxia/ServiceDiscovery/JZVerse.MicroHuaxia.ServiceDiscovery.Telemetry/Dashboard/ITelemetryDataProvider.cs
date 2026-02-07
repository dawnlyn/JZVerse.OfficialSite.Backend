using JZVerse.MicroHuaxia.ServiceDiscovery.Telemetry.Dashboard.Models;
using JZVerse.MicroHuaxia.ServiceDiscovery.Telemetry.Logging;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Telemetry.Dashboard;

/// <summary>
/// Dashboard 遥测数据提供者接口
/// </summary>
public interface ITelemetryDataProvider
{
    /// <summary>
    /// 查询日志
    /// </summary>
    LogQueryResult QueryLogs(LogQuery query);

    /// <summary>
    /// 获取日志统计
    /// </summary>
    LogStatistics GetLogStatistics();

    /// <summary>
    /// 获取最新日志 (用于实时流)
    /// </summary>
    IReadOnlyList<LogEntry> GetLatestLogs(int count = 100);

    /// <summary>
    /// 获取追踪列表
    /// </summary>
    Task<TraceListResult> GetTracesAsync(TraceQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取追踪详情
    /// </summary>
    Task<TraceDetail?> GetTraceDetailAsync(string traceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指标快照
    /// </summary>
    Task<MetricSnapshot> GetMetricsSnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指标时间序列
    /// </summary>
    Task<MetricTimeSeries> GetMetricTimeSeriesAsync(
        string metricName,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default
    );
}
