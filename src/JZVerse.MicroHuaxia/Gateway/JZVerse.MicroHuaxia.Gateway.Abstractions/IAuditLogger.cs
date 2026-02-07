namespace JZVerse.MicroHuaxia.Gateway.Abstractions;

/// <summary>
/// 审计日志记录器接口
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// 记录审计日志
    /// </summary>
    /// <param name="entry">审计日志条目</param>
    /// <param name="cancellationToken">取消令牌</param>
    ValueTask LogAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
}

/// <summary>
/// 审计日志存储接口
/// </summary>
public interface IAuditLogStore
{
    /// <summary>
    /// 保存审计日志
    /// </summary>
    Task SaveAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询审计日志
    /// </summary>
    Task<IReadOnlyList<AuditLogEntry>> QueryAsync(
        AuditLogQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取审计日志统计
    /// </summary>
    Task<AuditLogStatistics> GetStatisticsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 审计日志条目
/// </summary>
public sealed record AuditLogEntry
{
    /// <summary>
    /// 日志 ID
    /// </summary>
    public string LogId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 追踪 ID
    /// </summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// 请求时间
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 请求路径
    /// </summary>
    public required string RequestPath { get; init; }

    /// <summary>
    /// HTTP 方法
    /// </summary>
    public required string Method { get; init; }

    /// <summary>
    /// 客户端 IP
    /// </summary>
    public string? ClientIp { get; init; }

    /// <summary>
    /// 用户代理
    /// </summary>
    public string? UserAgent { get; init; }

    /// <summary>
    /// 用户 ID
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// 认证方式
    /// </summary>
    public string? AuthenticationMethod { get; init; }

    /// <summary>
    /// 匹配的路由 ID
    /// </summary>
    public string? RouteId { get; init; }

    /// <summary>
    /// 目标服务名称
    /// </summary>
    public string? TargetService { get; init; }

    /// <summary>
    /// 目标实例地址
    /// </summary>
    public string? TargetInstance { get; init; }

    /// <summary>
    /// 响应状态码
    /// </summary>
    public int StatusCode { get; init; }

    /// <summary>
    /// 请求处理耗时（毫秒）
    /// </summary>
    public long DurationMs { get; init; }

    /// <summary>
    /// 请求体大小（字节）
    /// </summary>
    public long? RequestBodySize { get; init; }

    /// <summary>
    /// 响应体大小（字节）
    /// </summary>
    public long? ResponseBodySize { get; init; }

    /// <summary>
    /// 是否被限流
    /// </summary>
    public bool WasRateLimited { get; init; }

    /// <summary>
    /// 是否命中缓存
    /// </summary>
    public bool WasCacheHit { get; init; }

    /// <summary>
    /// 是否被熔断
    /// </summary>
    public bool WasCircuitBroken { get; init; }

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; init; }

    /// <summary>
    /// 是否被舱壁拒绝
    /// </summary>
    public bool WasBulkheadRejected { get; init; }

    /// <summary>
    /// 是否执行了降级
    /// </summary>
    public bool FallbackExecuted { get; init; }

    /// <summary>
    /// 降级类型（static, cache, custom）
    /// </summary>
    public string? FallbackType { get; init; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 额外属性
    /// </summary>
    public Dictionary<string, string> Properties { get; init; } = new();
}

/// <summary>
/// 审计日志查询条件
/// </summary>
public sealed record AuditLogQuery
{
    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>
    /// 用户 ID
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// 客户端 IP
    /// </summary>
    public string? ClientIp { get; init; }

    /// <summary>
    /// 路由 ID
    /// </summary>
    public string? RouteId { get; init; }

    /// <summary>
    /// 目标服务
    /// </summary>
    public string? TargetService { get; init; }

    /// <summary>
    /// 状态码
    /// </summary>
    public int? StatusCode { get; init; }

    /// <summary>
    /// 最小状态码
    /// </summary>
    public int? MinStatusCode { get; init; }

    /// <summary>
    /// 最大状态码
    /// </summary>
    public int? MaxStatusCode { get; init; }

    /// <summary>
    /// 仅错误请求
    /// </summary>
    public bool OnlyErrors { get; init; }

    /// <summary>
    /// 跳过数量
    /// </summary>
    public int Skip { get; init; }

    /// <summary>
    /// 获取数量
    /// </summary>
    public int Take { get; init; } = 100;
}

/// <summary>
/// 审计日志统计
/// </summary>
public sealed record AuditLogStatistics
{
    /// <summary>
    /// 总请求数
    /// </summary>
    public long TotalRequests { get; init; }

    /// <summary>
    /// 成功请求数（2xx）
    /// </summary>
    public long SuccessfulRequests { get; init; }

    /// <summary>
    /// 客户端错误数（4xx）
    /// </summary>
    public long ClientErrors { get; init; }

    /// <summary>
    /// 服务器错误数（5xx）
    /// </summary>
    public long ServerErrors { get; init; }

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
    /// 限流请求数
    /// </summary>
    public long RateLimitedRequests { get; init; }

    /// <summary>
    /// 缓存命中数
    /// </summary>
    public long CacheHits { get; init; }

    /// <summary>
    /// 熔断请求数
    /// </summary>
    public long CircuitBrokenRequests { get; init; }

    /// <summary>
    /// 舱壁拒绝请求数
    /// </summary>
    public long BulkheadRejectedRequests { get; init; }

    /// <summary>
    /// 降级执行数
    /// </summary>
    public long FallbackExecutedRequests { get; init; }

    /// <summary>
    /// 按服务统计
    /// </summary>
    public Dictionary<string, long> RequestsByService { get; init; } = new();

    /// <summary>
    /// 按路由统计
    /// </summary>
    public Dictionary<string, long> RequestsByRoute { get; init; } = new();
}
