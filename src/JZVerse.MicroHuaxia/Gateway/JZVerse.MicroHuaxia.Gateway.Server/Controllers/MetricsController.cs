using JZVerse.MicroHuaxia.Gateway.Metrics.Api;
using JZVerse.MicroHuaxia.Gateway.Metrics.Api.Models;
using JZVerse.MicroHuaxia.Gateway.Metrics.Storage;
using Microsoft.AspNetCore.Mvc;

namespace JZVerse.MicroHuaxia.Gateway.Server.Controllers;

/// <summary>
/// 网关指标 API 控制器
/// </summary>
[ApiController]
[Route("api/gateway/metrics")]
public class MetricsController : ControllerBase
{
    private readonly IGatewayMetricsProvider _metricsProvider;
    private readonly IMetricsStore _store;

    /// <summary>
    /// 创建指标控制器
    /// </summary>
    public MetricsController(IGatewayMetricsProvider metricsProvider, IMetricsStore store)
    {
        _metricsProvider = metricsProvider;
        _store = store;
    }

    /// <summary>
    /// 获取当前指标快照
    /// </summary>
    [HttpGet("snapshot")]
    public async Task<ActionResult<MetricsSnapshot>> GetSnapshot(CancellationToken cancellationToken)
    {
        var snapshot = await _metricsProvider.GetSnapshotAsync(cancellationToken);
        return Ok(snapshot);
    }

    /// <summary>
    /// 获取指标时间序列
    /// </summary>
    /// <param name="name">指标名称</param>
    /// <param name="start">开始时间（ISO 8601 格式）</param>
    /// <param name="end">结束时间（ISO 8601 格式）</param>
    /// <param name="bucketSize">分桶大小（秒）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [HttpGet("{name}/timeseries")]
    public async Task<ActionResult<MetricTimeSeries>> GetTimeSeries(
        string name,
        [FromQuery] DateTimeOffset? start,
        [FromQuery] DateTimeOffset? end,
        [FromQuery] int? bucketSize,
        CancellationToken cancellationToken
    )
    {
        var timeSeries = await _metricsProvider.GetTimeSeriesAsync(
            name,
            start,
            end,
            null,
            bucketSize,
            cancellationToken
        );
        return Ok(timeSeries);
    }

    /// <summary>
    /// 获取指标统计信息
    /// </summary>
    /// <param name="start">开始时间（ISO 8601 格式）</param>
    /// <param name="end">结束时间（ISO 8601 格式）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [HttpGet("statistics")]
    public async Task<ActionResult<MetricsStatistics>> GetStatistics(
        [FromQuery] DateTimeOffset? start,
        [FromQuery] DateTimeOffset? end,
        CancellationToken cancellationToken
    )
    {
        var statistics = await _metricsProvider.GetStatisticsAsync(start, end, cancellationToken);
        return Ok(statistics);
    }

    /// <summary>
    /// 获取 Top N 路由（按指定指标排序）
    /// </summary>
    /// <param name="metricName">用于排序的指标名称</param>
    /// <param name="limit">返回数量限制（默认 10）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [HttpGet("routes/top")]
    public async Task<ActionResult<IReadOnlyList<RouteMetrics>>> GetTopRoutes(
        [FromQuery] string metricName = "gateway.requests.total",
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default
    )
    {
        var routes = await _metricsProvider.GetTopRoutesAsync(metricName, limit, cancellationToken);
        return Ok(routes);
    }

    /// <summary>
    /// 获取指定路由的指标
    /// </summary>
    /// <param name="routeId">路由 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    [HttpGet("routes/{routeId}")]
    public async Task<ActionResult<RouteMetrics>> GetRouteMetrics(
        string routeId,
        CancellationToken cancellationToken
    )
    {
        var metrics = await _metricsProvider.GetRouteMetricsAsync(routeId, cancellationToken);
        if (metrics == null)
        {
            return NotFound(new { error = "NotFound", message = $"Route '{routeId}' not found" });
        }
        return Ok(metrics);
    }

    /// <summary>
    /// 执行自定义查询
    /// </summary>
    /// <param name="query">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    [HttpPost("query")]
    public async Task<ActionResult<IReadOnlyList<MetricTimeSeries>>> Query(
        [FromBody] MetricsQuery query,
        CancellationToken cancellationToken
    )
    {
        var results = await _metricsProvider.QueryAsync(query, cancellationToken);
        return Ok(results);
    }

    /// <summary>
    /// 获取所有指标名称
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    [HttpGet("names")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetMetricNames(CancellationToken cancellationToken)
    {
        var names = await _metricsProvider.GetMetricNamesAsync(cancellationToken);
        return Ok(names);
    }

    /// <summary>
    /// 获取存储统计信息
    /// </summary>
    [HttpGet("store/statistics")]
    public ActionResult<MetricsStoreStatistics> GetStoreStatistics()
    {
        var statistics = _store.GetStatistics();
        return Ok(statistics);
    }

    /// <summary>
    /// 清理过期数据
    /// </summary>
    [HttpPost("store/cleanup")]
    public ActionResult Cleanup()
    {
        _store.Cleanup();
        return Ok(new { message = "Cleanup completed" });
    }

    /// <summary>
    /// 清空所有指标数据
    /// </summary>
    [HttpDelete("store/clear")]
    public ActionResult Clear()
    {
        _store.Clear();
        return Ok(new { message = "All metrics data cleared" });
    }
}
