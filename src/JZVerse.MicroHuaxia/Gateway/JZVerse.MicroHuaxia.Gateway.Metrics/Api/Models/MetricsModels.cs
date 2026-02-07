using JZVerse.MicroHuaxia.Gateway.Metrics.Storage;

namespace JZVerse.MicroHuaxia.Gateway.Metrics.Api.Models;

/// <summary>
/// 指标快照
/// </summary>
public sealed record MetricsSnapshot
{
    /// <summary>
    /// 快照时间
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 总请求数
    /// </summary>
    public long TotalRequests { get; init; }

    /// <summary>
    /// 活跃请求数
    /// </summary>
    public int ActiveRequests { get; init; }

    /// <summary>
    /// 平均响应时间（毫秒）
    /// </summary>
    public double AverageResponseTimeMs { get; init; }

    /// <summary>
    /// P50 响应时间（毫秒）
    /// </summary>
    public double P50ResponseTimeMs { get; init; }

    /// <summary>
    /// P95 响应时间（毫秒）
    /// </summary>
    public double P95ResponseTimeMs { get; init; }

    /// <summary>
    /// P99 响应时间（毫秒）
    /// </summary>
    public double P99ResponseTimeMs { get; init; }

    /// <summary>
    /// 错误率
    /// </summary>
    public double ErrorRate { get; init; }

    /// <summary>
    /// 缓存命中率
    /// </summary>
    public double CacheHitRate { get; init; }

    /// <summary>
    /// 限流拦截率
    /// </summary>
    public double RateLimitBlockedRate { get; init; }

    /// <summary>
    /// 打开的熔断器数量
    /// </summary>
    public int CircuitBreakerOpenCount { get; init; }

    /// <summary>
    /// 按状态码分组的请求数
    /// </summary>
    public Dictionary<string, long> RequestsByStatusGroup { get; init; } = [];

    /// <summary>
    /// 按路由分组的请求数（Top 10）
    /// </summary>
    public Dictionary<string, long> TopRoutesByRequests { get; init; } = [];
}

/// <summary>
/// 指标查询请求
/// </summary>
public sealed record MetricsQuery
{
    /// <summary>
    /// 指标名称
    /// </summary>
    public string? MetricName { get; init; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTimeOffset? StartTime { get; init; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTimeOffset? EndTime { get; init; }

    /// <summary>
    /// 标签过滤
    /// </summary>
    public Dictionary<string, string>? Tags { get; init; }

    /// <summary>
    /// 聚合类型
    /// </summary>
    public AggregationType? Aggregation { get; init; }

    /// <summary>
    /// 聚合窗口大小（秒）
    /// </summary>
    public int? BucketSizeSeconds { get; init; }

    /// <summary>
    /// 路由 ID 过滤
    /// </summary>
    public string? RouteId { get; init; }

    /// <summary>
    /// 限制返回数量
    /// </summary>
    public int? Limit { get; init; }
}

/// <summary>
/// 指标时间序列
/// </summary>
public sealed record MetricTimeSeries
{
    /// <summary>
    /// 指标名称
    /// </summary>
    public required string MetricName { get; init; }

    /// <summary>
    /// 标签
    /// </summary>
    public IReadOnlyDictionary<string, object?> Tags { get; init; } = new Dictionary<string, object?>();

    /// <summary>
    /// 数据点列表
    /// </summary>
    public IReadOnlyList<TimeSeriesDataPoint> DataPoints { get; init; } = [];

    /// <summary>
    /// 聚合类型
    /// </summary>
    public AggregationType? Aggregation { get; init; }
}

/// <summary>
/// 时间序列数据点
/// </summary>
public sealed record TimeSeriesDataPoint
{
    /// <summary>
    /// 时间戳
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// 值
    /// </summary>
    public required double Value { get; init; }
}

/// <summary>
/// 指标统计信息
/// </summary>
public sealed record MetricsStatistics
{
    /// <summary>
    /// 统计时间范围
    /// </summary>
    public required DateTimeOffset StartTime { get; init; }

    /// <summary>
    /// 统计时间范围
    /// </summary>
    public required DateTimeOffset EndTime { get; init; }

    /// <summary>
    /// 请求统计
    /// </summary>
    public required RequestStatistics Requests { get; init; }

    /// <summary>
    /// 认证统计
    /// </summary>
    public required AuthStatistics Auth { get; init; }

    /// <summary>
    /// 限流统计
    /// </summary>
    public required RateLimitStatistics RateLimit { get; init; }

    /// <summary>
    /// 缓存统计
    /// </summary>
    public required CacheStatistics Cache { get; init; }

    /// <summary>
    /// 转发统计
    /// </summary>
    public required ForwardStatistics Forward { get; init; }

    /// <summary>
    /// 弹性统计
    /// </summary>
    public required ResilienceStatistics Resilience { get; init; }

    /// <summary>
    /// 存储统计
    /// </summary>
    public required MetricsStoreStatistics Store { get; init; }
}

/// <summary>
/// 请求统计
/// </summary>
public sealed record RequestStatistics
{
    /// <summary>
    /// 总请求数
    /// </summary>
    public long Total { get; init; }

    /// <summary>
    /// 成功请求数
    /// </summary>
    public long Success { get; init; }

    /// <summary>
    /// 错误请求数
    /// </summary>
    public long Errors { get; init; }

    /// <summary>
    /// 平均响应时间（毫秒）
    /// </summary>
    public double AverageResponseTimeMs { get; init; }

    /// <summary>
    /// P50 响应时间（毫秒）
    /// </summary>
    public double P50ResponseTimeMs { get; init; }

    /// <summary>
    /// P95 响应时间（毫秒）
    /// </summary>
    public double P95ResponseTimeMs { get; init; }

    /// <summary>
    /// P99 响应时间（毫秒）
    /// </summary>
    public double P99ResponseTimeMs { get; init; }

    /// <summary>
    /// 按状态码分组
    /// </summary>
    public Dictionary<int, long> ByStatusCode { get; init; } = [];

    /// <summary>
    /// 按方法分组
    /// </summary>
    public Dictionary<string, long> ByMethod { get; init; } = [];
}

/// <summary>
/// 认证统计
/// </summary>
public sealed record AuthStatistics
{
    /// <summary>
    /// 总尝试次数
    /// </summary>
    public long TotalAttempts { get; init; }

    /// <summary>
    /// 成功次数
    /// </summary>
    public long Success { get; init; }

    /// <summary>
    /// 失败次数
    /// </summary>
    public long Failures { get; init; }

    /// <summary>
    /// 成功率
    /// </summary>
    public double SuccessRate { get; init; }

    /// <summary>
    /// 平均耗时（毫秒）
    /// </summary>
    public double AverageDurationMs { get; init; }

    /// <summary>
    /// 按策略分组
    /// </summary>
    public Dictionary<string, long> ByStrategy { get; init; } = [];
}

/// <summary>
/// 限流统计
/// </summary>
public sealed record RateLimitStatistics
{
    /// <summary>
    /// 允许通过数
    /// </summary>
    public long Allowed { get; init; }

    /// <summary>
    /// 拦截数
    /// </summary>
    public long Blocked { get; init; }

    /// <summary>
    /// 拦截率
    /// </summary>
    public double BlockedRate { get; init; }

    /// <summary>
    /// 按算法分组
    /// </summary>
    public Dictionary<string, long> ByAlgorithm { get; init; } = [];
}

/// <summary>
/// 缓存统计
/// </summary>
public sealed record CacheStatistics
{
    /// <summary>
    /// 命中数
    /// </summary>
    public long Hits { get; init; }

    /// <summary>
    /// 未命中数
    /// </summary>
    public long Misses { get; init; }

    /// <summary>
    /// 命中率
    /// </summary>
    public double HitRate { get; init; }

    /// <summary>
    /// 当前大小
    /// </summary>
    public int CurrentSize { get; init; }

    /// <summary>
    /// 驱逐数
    /// </summary>
    public long Evictions { get; init; }
}

/// <summary>
/// 转发统计
/// </summary>
public sealed record ForwardStatistics
{
    /// <summary>
    /// 总尝试次数
    /// </summary>
    public long TotalAttempts { get; init; }

    /// <summary>
    /// 成功次数
    /// </summary>
    public long Success { get; init; }

    /// <summary>
    /// 失败次数
    /// </summary>
    public long Failures { get; init; }

    /// <summary>
    /// 成功率
    /// </summary>
    public double SuccessRate { get; init; }

    /// <summary>
    /// 平均耗时（毫秒）
    /// </summary>
    public double AverageDurationMs { get; init; }

    /// <summary>
    /// 按协议分组
    /// </summary>
    public Dictionary<string, long> ByProtocol { get; init; } = [];
}

/// <summary>
/// 弹性统计
/// </summary>
public sealed record ResilienceStatistics
{
    /// <summary>
    /// 重试次数
    /// </summary>
    public long RetryAttempts { get; init; }

    /// <summary>
    /// 熔断触发次数
    /// </summary>
    public long CircuitBreakerTrips { get; init; }

    /// <summary>
    /// 舱壁拒绝次数
    /// </summary>
    public long BulkheadRejections { get; init; }

    /// <summary>
    /// 降级执行次数
    /// </summary>
    public long FallbackExecutions { get; init; }

    /// <summary>
    /// 超时次数
    /// </summary>
    public long Timeouts { get; init; }
}

/// <summary>
/// 路由指标
/// </summary>
public sealed record RouteMetrics
{
    /// <summary>
    /// 路由 ID
    /// </summary>
    public required string RouteId { get; init; }

    /// <summary>
    /// 总请求数
    /// </summary>
    public long TotalRequests { get; init; }

    /// <summary>
    /// 错误数
    /// </summary>
    public long Errors { get; init; }

    /// <summary>
    /// 错误率
    /// </summary>
    public double ErrorRate { get; init; }

    /// <summary>
    /// 平均响应时间（毫秒）
    /// </summary>
    public double AverageResponseTimeMs { get; init; }

    /// <summary>
    /// P95 响应时间（毫秒）
    /// </summary>
    public double P95ResponseTimeMs { get; init; }

    /// <summary>
    /// 缓存命中率
    /// </summary>
    public double CacheHitRate { get; init; }

    /// <summary>
    /// 限流拦截数
    /// </summary>
    public long RateLimitBlocked { get; init; }
}
