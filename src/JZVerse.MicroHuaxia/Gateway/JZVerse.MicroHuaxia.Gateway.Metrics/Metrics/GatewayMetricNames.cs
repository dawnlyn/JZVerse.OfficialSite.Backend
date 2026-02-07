namespace JZVerse.MicroHuaxia.Gateway.Metrics;

/// <summary>
/// 网关指标名称常量
/// 遵循 OpenTelemetry 语义约定，使用 gateway. 前缀
/// </summary>
public static class GatewayMetricNames
{
    /// <summary>
    /// Meter 名称
    /// </summary>
    public const string MeterName = "JZVerse.MicroHuaxia.Gateway";

    /// <summary>
    /// Meter 版本
    /// </summary>
    public const string Version = "1.0.0";

    // ==================== 请求指标 ====================

    /// <summary>
    /// 请求总数（计数器）
    /// </summary>
    public const string RequestsTotal = "gateway.requests.total";

    /// <summary>
    /// 请求耗时（直方图，毫秒）
    /// </summary>
    public const string RequestDuration = "gateway.request.duration";

    /// <summary>
    /// 请求体大小（直方图，字节）
    /// </summary>
    public const string RequestSize = "gateway.request.size";

    /// <summary>
    /// 响应体大小（直方图，字节）
    /// </summary>
    public const string ResponseSize = "gateway.response.size";

    /// <summary>
    /// 活跃请求数（可观察计数器）
    /// </summary>
    public const string ActiveRequests = "gateway.requests.active";

    // ==================== 认证指标 ====================

    /// <summary>
    /// 认证尝试总数（计数器）
    /// </summary>
    public const string AuthAttempts = "gateway.auth.attempts";

    /// <summary>
    /// 认证失败总数（计数器）
    /// </summary>
    public const string AuthFailures = "gateway.auth.failures";

    /// <summary>
    /// 认证耗时（直方图，毫秒）
    /// </summary>
    public const string AuthDuration = "gateway.auth.duration";

    // ==================== 限流指标 ====================

    /// <summary>
    /// 限流允许通过数（计数器）
    /// </summary>
    public const string RateLimitAllowed = "gateway.ratelimit.allowed";

    /// <summary>
    /// 限流拒绝数（计数器）
    /// </summary>
    public const string RateLimitBlocked = "gateway.ratelimit.blocked";

    /// <summary>
    /// 限流剩余配额（可观察计数器）
    /// </summary>
    public const string RateLimitRemaining = "gateway.ratelimit.remaining";

    // ==================== 缓存指标 ====================

    /// <summary>
    /// 缓存命中数（计数器）
    /// </summary>
    public const string CacheHits = "gateway.cache.hits";

    /// <summary>
    /// 缓存未命中数（计数器）
    /// </summary>
    public const string CacheMisses = "gateway.cache.misses";

    /// <summary>
    /// 缓存条目数（可观察计数器）
    /// </summary>
    public const string CacheSize = "gateway.cache.size";

    /// <summary>
    /// 缓存驱逐数（计数器）
    /// </summary>
    public const string CacheEvictions = "gateway.cache.evictions";

    /// <summary>
    /// 缓存命中率（可观察计数器）
    /// </summary>
    public const string CacheHitRate = "gateway.cache.hit_rate";

    // ==================== 转发指标 ====================

    /// <summary>
    /// 转发尝试数（计数器）
    /// </summary>
    public const string ForwardAttempts = "gateway.forward.attempts";

    /// <summary>
    /// 转发失败数（计数器）
    /// </summary>
    public const string ForwardFailures = "gateway.forward.failures";

    /// <summary>
    /// 转发耗时（直方图，毫秒）
    /// </summary>
    public const string ForwardDuration = "gateway.forward.duration";

    // ==================== 弹性指标 ====================

    /// <summary>
    /// 重试次数（计数器）
    /// </summary>
    public const string RetryAttempts = "gateway.retry.attempts";

    /// <summary>
    /// 熔断器状态（可观察计数器：0=Closed, 1=Open, 2=HalfOpen）
    /// </summary>
    public const string CircuitBreakerState = "gateway.circuitbreaker.state";

    /// <summary>
    /// 熔断触发数（计数器）
    /// </summary>
    public const string CircuitBreakerTrips = "gateway.circuitbreaker.trips";

    /// <summary>
    /// 舱壁并发数（可观察计数器）
    /// </summary>
    public const string BulkheadConcurrent = "gateway.bulkhead.concurrent";

    /// <summary>
    /// 舱壁队列长度（可观察计数器）
    /// </summary>
    public const string BulkheadQueueLength = "gateway.bulkhead.queue_length";

    /// <summary>
    /// 舱壁拒绝数（计数器）
    /// </summary>
    public const string BulkheadRejected = "gateway.bulkhead.rejected";

    /// <summary>
    /// 降级执行数（计数器）
    /// </summary>
    public const string FallbackExecutions = "gateway.fallback.executions";

    // ==================== 错误指标 ====================

    /// <summary>
    /// 错误总数（计数器）
    /// </summary>
    public const string ErrorsTotal = "gateway.errors.total";

    /// <summary>
    /// 超时总数（计数器）
    /// </summary>
    public const string TimeoutsTotal = "gateway.timeouts.total";
}
