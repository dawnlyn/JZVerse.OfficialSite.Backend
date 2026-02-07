using JZVerse.MicroHuaxia.ServiceDiscovery.Telemetry.Dashboard;
using JZVerse.MicroHuaxia.ServiceDiscovery.Telemetry.Dashboard.Models;
using JZVerse.MicroHuaxia.ServiceDiscovery.Telemetry.Logging;
using Microsoft.AspNetCore.Mvc;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Server.Controllers;

/// <summary>
/// Telemetry API 控制器
/// </summary>
[ApiController]
[Route("api/v1/telemetry")]
public class TelemetryController(
    ILogger<TelemetryController> _logger,
    ITelemetryDataProvider? _telemetryProvider = null
) : ControllerBase
{
    /// <summary>
    /// 检查 Telemetry 是否可用
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus() =>
        Ok(new { Enabled = _telemetryProvider != null, Timestamp = DateTimeOffset.UtcNow });

    /// <summary>
    /// 查询日志
    /// </summary>
    [HttpPost("logs/query")]
    public IActionResult QueryLogs([FromBody] LogQuery query)
    {
        if (_telemetryProvider == null)
        {
            return NotFound(new { Error = "Telemetry not enabled" });
        }

        var result = _telemetryProvider.QueryLogs(query);
        return Ok(result);
    }

    /// <summary>
    /// 获取最新日志
    /// </summary>
    [HttpGet("logs/latest")]
    public IActionResult GetLatestLogs([FromQuery] int count = 100)
    {
        if (_telemetryProvider == null)
        {
            return NotFound(new { Error = "Telemetry not enabled" });
        }

        var logs = _telemetryProvider.GetLatestLogs(count);
        return Ok(logs);
    }

    /// <summary>
    /// 获取日志统计
    /// </summary>
    [HttpGet("logs/statistics")]
    public IActionResult GetLogStatistics()
    {
        if (_telemetryProvider == null)
        {
            return NotFound(new { Error = "Telemetry not enabled" });
        }

        var stats = _telemetryProvider.GetLogStatistics();
        return Ok(stats);
    }

    /// <summary>
    /// 获取追踪列表
    /// </summary>
    [HttpPost("traces/query")]
    public async Task<IActionResult> GetTraces([FromBody] TraceQuery query, CancellationToken cancellationToken)
    {
        if (_telemetryProvider == null)
        {
            return NotFound(new { Error = "Telemetry not enabled" });
        }

        var result = await _telemetryProvider.GetTracesAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// 获取追踪详情
    /// </summary>
    [HttpGet("traces/{traceId}")]
    public async Task<IActionResult> GetTraceDetail(string traceId, CancellationToken cancellationToken)
    {
        if (_telemetryProvider == null)
        {
            return NotFound(new { Error = "Telemetry not enabled" });
        }

        var detail = await _telemetryProvider.GetTraceDetailAsync(traceId, cancellationToken);
        if (detail == null)
        {
            return NotFound();
        }
        return Ok(detail);
    }

    /// <summary>
    /// 获取指标快照
    /// </summary>
    [HttpGet("metrics/snapshot")]
    public async Task<IActionResult> GetMetricsSnapshot(CancellationToken cancellationToken)
    {
        if (_telemetryProvider == null)
        {
            return NotFound(new { Error = "Telemetry not enabled" });
        }

        var snapshot = await _telemetryProvider.GetMetricsSnapshotAsync(cancellationToken);
        return Ok(snapshot);
    }

    /// <summary>
    /// 获取指标时间序列
    /// </summary>
    [HttpGet("metrics/{metricName}/timeseries")]
    public async Task<IActionResult> GetMetricTimeSeries(
        string metricName,
        [FromQuery] DateTimeOffset? start,
        [FromQuery] DateTimeOffset? end,
        CancellationToken cancellationToken
    )
    {
        if (_telemetryProvider == null)
        {
            return NotFound(new { Error = "Telemetry not enabled" });
        }

        var startTime = start ?? DateTimeOffset.UtcNow.AddHours(-1);
        var endTime = end ?? DateTimeOffset.UtcNow;

        var timeSeries = await _telemetryProvider.GetMetricTimeSeriesAsync(
            metricName,
            startTime,
            endTime,
            cancellationToken
        );

        return Ok(timeSeries);
    }
}
