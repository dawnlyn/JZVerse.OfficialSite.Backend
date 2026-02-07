namespace JZVerse.MicroHuaxia.Gateway.Metrics;

/// <summary>
/// 网关指标标签名称常量
/// 用于维度拆分和查询过滤
/// </summary>
public static class GatewayMetricTags
{
    // ==================== 通用标签 ====================

    /// <summary>
    /// 路由 ID
    /// </summary>
    public const string RouteId = "route.id";

    /// <summary>
    /// 路由名称
    /// </summary>
    public const string RouteName = "route.name";

    /// <summary>
    /// HTTP 方法
    /// </summary>
    public const string HttpMethod = "http.method";

    /// <summary>
    /// HTTP 状态码
    /// </summary>
    public const string HttpStatusCode = "http.status_code";

    /// <summary>
    /// 请求路径
    /// </summary>
    public const string HttpPath = "http.path";

    /// <summary>
    /// 错误类型
    /// </summary>
    public const string ErrorType = "error.type";

    /// <summary>
    /// 服务名称
    /// </summary>
    public const string ServiceName = "service.name";

    // ==================== 认证标签 ====================

    /// <summary>
    /// 认证策略（jwt/apikey/basic）
    /// </summary>
    public const string AuthStrategy = "auth.strategy";

    /// <summary>
    /// 认证结果（success/failure）
    /// </summary>
    public const string AuthResult = "auth.result";

    /// <summary>
    /// 认证失败原因
    /// </summary>
    public const string AuthFailureReason = "auth.failure_reason";

    // ==================== 限流标签 ====================

    /// <summary>
    /// 限流算法（sliding_window/token_bucket/fixed_window）
    /// </summary>
    public const string RateLimitAlgorithm = "ratelimit.algorithm";

    /// <summary>
    /// 限流键策略（ip/route/user/global）
    /// </summary>
    public const string RateLimitKeyStrategy = "ratelimit.key_strategy";

    /// <summary>
    /// 限流结果（allowed/blocked）
    /// </summary>
    public const string RateLimitResult = "ratelimit.result";

    // ==================== 缓存标签 ====================

    /// <summary>
    /// 缓存结果（hit/miss）
    /// </summary>
    public const string CacheResult = "cache.result";

    /// <summary>
    /// 缓存操作（read/write/evict）
    /// </summary>
    public const string CacheOperation = "cache.operation";

    // ==================== 转发标签 ====================

    /// <summary>
    /// 转发协议（http/grpc/websocket）
    /// </summary>
    public const string ForwardProtocol = "forward.protocol";

    /// <summary>
    /// 转发目标地址
    /// </summary>
    public const string ForwardTarget = "forward.target";

    /// <summary>
    /// 转发结果（success/failure）
    /// </summary>
    public const string ForwardResult = "forward.result";

    /// <summary>
    /// 目标服务名称
    /// </summary>
    public const string ForwardServiceName = "forward.service_name";

    // ==================== 弹性标签 ====================

    /// <summary>
    /// 重试次数
    /// </summary>
    public const string RetryAttempt = "retry.attempt";

    /// <summary>
    /// 重试原因
    /// </summary>
    public const string RetryReason = "retry.reason";

    /// <summary>
    /// 熔断器名称
    /// </summary>
    public const string CircuitBreakerName = "circuitbreaker.name";

    /// <summary>
    /// 熔断器状态（closed/open/half_open）
    /// </summary>
    public const string CircuitBreakerState = "circuitbreaker.state";

    /// <summary>
    /// 舱壁名称
    /// </summary>
    public const string BulkheadName = "bulkhead.name";

    /// <summary>
    /// 降级类型（static/cache）
    /// </summary>
    public const string FallbackType = "fallback.type";

    /// <summary>
    /// 降级原因
    /// </summary>
    public const string FallbackReason = "fallback.reason";

    // ==================== 状态码分组标签 ====================

    /// <summary>
    /// 状态码分组（1xx/2xx/3xx/4xx/5xx）
    /// </summary>
    public const string StatusCodeGroup = "http.status_code_group";

    /// <summary>
    /// 是否成功（基于状态码）
    /// </summary>
    public const string IsSuccess = "is_success";
}
