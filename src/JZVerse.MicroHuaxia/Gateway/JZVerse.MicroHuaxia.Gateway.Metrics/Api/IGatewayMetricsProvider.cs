using JZVerse.MicroHuaxia.Gateway.Metrics.Api.Models;

namespace JZVerse.MicroHuaxia.Gateway.Metrics.Api;

/// <summary>
/// 网关指标数据提供者接口
/// </summary>
public interface IGatewayMetricsProvider
{
    /// <summary>
    /// 获取当前指标快照
    /// </summary>
    Task<MetricsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指标时间序列
    /// </summary>
    Task<MetricTimeSeries> GetTimeSeriesAsync(
        string metricName,
        DateTimeOffset? start = null,
        DateTimeOffset? end = null,
        Dictionary<string, string>? tags = null,
        int? bucketSizeSeconds = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 获取指标统计信息
    /// </summary>
    Task<MetricsStatistics> GetStatisticsAsync(
        DateTimeOffset? start = null,
        DateTimeOffset? end = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 获取 Top N 路由（按指定指标排序）
    /// </summary>
    Task<IReadOnlyList<RouteMetrics>> GetTopRoutesAsync(
        string metricName,
        int limit = 10,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 获取指定路由的指标
    /// </summary>
    Task<RouteMetrics?> GetRouteMetricsAsync(string routeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行自定义查询
    /// </summary>
    Task<IReadOnlyList<MetricTimeSeries>> QueryAsync(
        MetricsQuery query,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 获取所有指标名称
    /// </summary>
    Task<IReadOnlyList<string>> GetMetricNamesAsync(CancellationToken cancellationToken = default);
}
