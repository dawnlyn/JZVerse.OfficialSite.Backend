using System.Diagnostics;

namespace JZVerse.MicroHuaxia.Gateway.Tracing.Storage;

/// <summary>
/// Span 事件
/// </summary>
public sealed record SpanEvent
{
    /// <summary>
    /// 事件名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 事件时间
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 事件属性
    /// </summary>
    public Dictionary<string, object?> Attributes { get; init; } = [];
}

/// <summary>
/// 追踪 Span 数据模型
/// </summary>
public sealed record TraceSpan
{
    /// <summary>
    /// 追踪 ID
    /// </summary>
    public required string TraceId { get; init; }

    /// <summary>
    /// Span ID
    /// </summary>
    public required string SpanId { get; init; }

    /// <summary>
    /// 父 Span ID（可选）
    /// </summary>
    public string? ParentSpanId { get; init; }

    /// <summary>
    /// Span 名称（操作名称）
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Span 类型
    /// </summary>
    public ActivityKind Kind { get; init; } = ActivityKind.Internal;

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTimeOffset StartTime { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTimeOffset? EndTime { get; init; }

    /// <summary>
    /// 持续时间
    /// </summary>
    public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;

    /// <summary>
    /// 状态码
    /// </summary>
    public ActivityStatusCode Status { get; init; } = ActivityStatusCode.Unset;

    /// <summary>
    /// 状态描述
    /// </summary>
    public string? StatusDescription { get; init; }

    /// <summary>
    /// Span 属性
    /// </summary>
    public Dictionary<string, object?> Attributes { get; init; } = [];

    /// <summary>
    /// Span 事件列表
    /// </summary>
    public List<SpanEvent> Events { get; init; } = [];

    /// <summary>
    /// 服务名称
    /// </summary>
    public string ServiceName { get; init; } = GatewayActivitySource.Name;

    /// <summary>
    /// 从 Activity 创建 TraceSpan
    /// </summary>
    public static TraceSpan FromActivity(Activity activity)
    {
        var attributes = new Dictionary<string, object?>();
        foreach (var tag in activity.Tags)
        {
            attributes[tag.Key] = tag.Value;
        }

        var events = activity
            .Events.Select(e => new SpanEvent
            {
                Name = e.Name,
                Timestamp = e.Timestamp,
                Attributes = e.Tags.ToDictionary(t => t.Key, t => (object?)t.Value),
            })
            .ToList();

        return new TraceSpan
        {
            TraceId = activity.TraceId.ToHexString(),
            SpanId = activity.SpanId.ToHexString(),
            ParentSpanId = activity.ParentSpanId == default ? null : activity.ParentSpanId.ToHexString(),
            Name = activity.OperationName,
            Kind = activity.Kind,
            StartTime = activity.StartTimeUtc,
            EndTime = activity.StartTimeUtc + activity.Duration,
            Status = activity.Status,
            StatusDescription = activity.StatusDescription,
            Attributes = attributes,
            Events = events,
            ServiceName = activity.Source.Name,
        };
    }
}

/// <summary>
/// 追踪摘要信息
/// </summary>
public sealed record TraceSummary
{
    /// <summary>
    /// 追踪 ID
    /// </summary>
    public required string TraceId { get; init; }

    /// <summary>
    /// 根 Span 名称
    /// </summary>
    public string? RootSpanName { get; init; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTimeOffset StartTime { get; init; }

    /// <summary>
    /// 总持续时间
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Span 数量
    /// </summary>
    public int SpanCount { get; init; }

    /// <summary>
    /// 涉及的服务列表
    /// </summary>
    public List<string> Services { get; init; } = [];

    /// <summary>
    /// 是否有错误
    /// </summary>
    public bool HasError { get; init; }

    /// <summary>
    /// HTTP 状态码（如果有）
    /// </summary>
    public int? HttpStatusCode { get; init; }
}

/// <summary>
/// 追踪查询条件
/// </summary>
public sealed class TraceQuery
{
    /// <summary>
    /// 服务名称
    /// </summary>
    public string? ServiceName { get; set; }

    /// <summary>
    /// 操作名称
    /// </summary>
    public string? OperationName { get; set; }

    /// <summary>
    /// 开始时间（>=）
    /// </summary>
    public DateTimeOffset? StartTimeFrom { get; set; }

    /// <summary>
    /// 开始时间（<=）
    /// </summary>
    public DateTimeOffset? StartTimeTo { get; set; }

    /// <summary>
    /// 最小持续时间
    /// </summary>
    public TimeSpan? MinDuration { get; set; }

    /// <summary>
    /// 最大持续时间
    /// </summary>
    public TimeSpan? MaxDuration { get; set; }

    /// <summary>
    /// 仅显示错误
    /// </summary>
    public bool? HasError { get; set; }

    /// <summary>
    /// HTTP 状态码
    /// </summary>
    public int? HttpStatusCode { get; set; }

    /// <summary>
    /// 属性过滤（key=value）
    /// </summary>
    public Dictionary<string, string> Attributes { get; set; } = [];

    /// <summary>
    /// 分页：跳过数量
    /// </summary>
    public int Skip { get; set; } = 0;

    /// <summary>
    /// 分页：获取数量（默认 20，最大 100）
    /// </summary>
    public int Take { get; set; } = 20;

    /// <summary>
    /// 排序字段
    /// </summary>
    public TraceQuerySortField SortBy { get; set; } = TraceQuerySortField.StartTime;

    /// <summary>
    /// 是否降序
    /// </summary>
    public bool SortDescending { get; set; } = true;
}

/// <summary>
/// 追踪查询排序字段
/// </summary>
public enum TraceQuerySortField
{
    StartTime,
    Duration,
    SpanCount,
}

/// <summary>
/// 追踪查询结果
/// </summary>
public sealed record TraceQueryResult
{
    /// <summary>
    /// 追踪摘要列表
    /// </summary>
    public IReadOnlyList<TraceSummary> Traces { get; init; } = [];

    /// <summary>
    /// 总数
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// 是否有更多数据
    /// </summary>
    public bool HasMore { get; init; }
}

/// <summary>
/// 追踪统计信息
/// </summary>
public sealed record TraceStatistics
{
    /// <summary>
    /// 总追踪数
    /// </summary>
    public long TotalTraces { get; init; }

    /// <summary>
    /// 总 Span 数
    /// </summary>
    public long TotalSpans { get; init; }

    /// <summary>
    /// 错误追踪数
    /// </summary>
    public long ErrorTraces { get; init; }

    /// <summary>
    /// 平均响应时间（毫秒）
    /// </summary>
    public double AverageDurationMs { get; init; }

    /// <summary>
    /// P50 响应时间（毫秒）
    /// </summary>
    public double P50DurationMs { get; init; }

    /// <summary>
    /// P95 响应时间（毫秒）
    /// </summary>
    public double P95DurationMs { get; init; }

    /// <summary>
    /// P99 响应时间（毫秒）
    /// </summary>
    public double P99DurationMs { get; init; }

    /// <summary>
    /// 按服务统计
    /// </summary>
    public Dictionary<string, long> TracesByService { get; init; } = [];

    /// <summary>
    /// 按操作统计
    /// </summary>
    public Dictionary<string, long> TracesByOperation { get; init; } = [];
}
