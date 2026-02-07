namespace JZVerse.MicroHuaxia.ServiceDiscovery.Telemetry.Dashboard.Models;

/// <summary>
/// 追踪查询条件
/// </summary>
public sealed class TraceQuery
{
    /// <summary>
    /// 服务名称过滤
    /// </summary>
    public string? ServiceName { get; set; }

    /// <summary>
    /// 操作名称过滤
    /// </summary>
    public string? Operation { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTimeOffset? StartTime { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>
    /// 最小耗时 (毫秒)
    /// </summary>
    public int? MinDurationMs { get; set; }

    /// <summary>
    /// 是否包含错误
    /// </summary>
    public bool? HasErrors { get; set; }

    /// <summary>
    /// 跳过条数
    /// </summary>
    public int Skip { get; set; }

    /// <summary>
    /// 获取条数
    /// </summary>
    public int Take { get; set; } = 50;
}

/// <summary>
/// 追踪列表结果
/// </summary>
public sealed class TraceListResult
{
    /// <summary>
    /// 追踪摘要列表
    /// </summary>
    public IReadOnlyList<TraceSummary> Traces { get; init; } = [];

    /// <summary>
    /// 总数
    /// </summary>
    public int TotalCount { get; init; }
}

/// <summary>
/// 追踪摘要
/// </summary>
public sealed class TraceSummary
{
    /// <summary>
    /// Trace ID
    /// </summary>
    public string TraceId { get; init; } = string.Empty;

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTimeOffset StartTime { get; init; }

    /// <summary>
    /// 总耗时 (毫秒)
    /// </summary>
    public double DurationMs { get; init; }

    /// <summary>
    /// 根操作名称
    /// </summary>
    public string RootOperation { get; init; } = string.Empty;

    /// <summary>
    /// Span 数量
    /// </summary>
    public int SpanCount { get; init; }

    /// <summary>
    /// 是否包含错误
    /// </summary>
    public bool HasErrors { get; init; }

    /// <summary>
    /// 涉及的服务列表
    /// </summary>
    public IReadOnlyList<string> Services { get; init; } = [];
}

/// <summary>
/// 追踪详情
/// </summary>
public sealed class TraceDetail
{
    /// <summary>
    /// Trace ID
    /// </summary>
    public string TraceId { get; init; } = string.Empty;

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTimeOffset StartTime { get; init; }

    /// <summary>
    /// 总耗时 (毫秒)
    /// </summary>
    public double TotalDurationMs { get; init; }

    /// <summary>
    /// Span 列表
    /// </summary>
    public IReadOnlyList<SpanDetail> Spans { get; init; } = [];
}

/// <summary>
/// Span 详情
/// </summary>
public sealed class SpanDetail
{
    /// <summary>
    /// Span ID
    /// </summary>
    public string SpanId { get; init; } = string.Empty;

    /// <summary>
    /// 父 Span ID
    /// </summary>
    public string? ParentSpanId { get; init; }

    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 类型
    /// </summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTimeOffset StartTime { get; init; }

    /// <summary>
    /// 耗时 (毫秒)
    /// </summary>
    public double DurationMs { get; init; }

    /// <summary>
    /// 状态
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// 属性
    /// </summary>
    public IReadOnlyDictionary<string, object?> Attributes { get; init; } = new Dictionary<string, object?>();

    /// <summary>
    /// 事件列表
    /// </summary>
    public IReadOnlyList<SpanEvent> Events { get; init; } = [];
}

/// <summary>
/// Span 事件
/// </summary>
public sealed class SpanEvent
{
    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// 属性
    /// </summary>
    public IReadOnlyDictionary<string, object?> Attributes { get; init; } = new Dictionary<string, object?>();
}

/// <summary>
/// 指标快照
/// </summary>
public sealed class MetricSnapshot
{
    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 服务总数
    /// </summary>
    public int TotalServices { get; init; }

    /// <summary>
    /// 实例总数
    /// </summary>
    public int TotalInstances { get; init; }

    /// <summary>
    /// 健康实例数
    /// </summary>
    public int HealthyInstances { get; init; }

    /// <summary>
    /// 不健康实例数
    /// </summary>
    public int UnhealthyInstances { get; init; }

    /// <summary>
    /// 健康率
    /// </summary>
    public double HealthRate => TotalInstances > 0 ? (double)HealthyInstances / TotalInstances : 0;
}

/// <summary>
/// 指标时间序列
/// </summary>
public sealed class MetricTimeSeries
{
    /// <summary>
    /// 指标名称
    /// </summary>
    public string MetricName { get; init; } = string.Empty;

    /// <summary>
    /// 数据点列表
    /// </summary>
    public IReadOnlyList<MetricDataPoint> DataPoints { get; init; } = [];
}

/// <summary>
/// 指标数据点
/// </summary>
public sealed class MetricDataPoint
{
    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// 值
    /// </summary>
    public double Value { get; init; }

    /// <summary>
    /// 标签
    /// </summary>
    public IReadOnlyDictionary<string, string> Labels { get; init; } = new Dictionary<string, string>();
}
