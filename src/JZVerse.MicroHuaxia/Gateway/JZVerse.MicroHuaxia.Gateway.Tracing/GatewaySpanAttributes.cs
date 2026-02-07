namespace JZVerse.MicroHuaxia.Gateway.Tracing;

/// <summary>
/// 网关追踪 Span 属性常量
/// 遵循 OpenTelemetry 语义约定
/// </summary>
public static class GatewaySpanAttributes
{
    // ==================== 通用属性 ====================

    /// <summary>
    /// 路由 ID
    /// </summary>
    public const string RouteId = "gateway.route.id";

    /// <summary>
    /// 路由名称
    /// </summary>
    public const string RouteName = "gateway.route.name";

    /// <summary>
    /// 网关请求 ID
    /// </summary>
    public const string RequestId = "gateway.request.id";

    /// <summary>
    /// 操作名称
    /// </summary>
    public const string Operation = "gateway.operation";

    /// <summary>
    /// 操作结果
    /// </summary>
    public const string OperationResult = "gateway.operation.result";

    // ==================== 路由匹配属性 ====================

    /// <summary>
    /// 匹配的路径模式
    /// </summary>
    public const string RoutingPattern = "gateway.routing.pattern";

    /// <summary>
    /// HTTP 方法
    /// </summary>
    public const string RoutingMethod = "gateway.routing.method";

    /// <summary>
    /// 路由匹配耗时（毫秒）
    /// </summary>
    public const string RoutingDurationMs = "gateway.routing.duration_ms";

    /// <summary>
    /// 是否匹配成功
    /// </summary>
    public const string RoutingMatched = "gateway.routing.matched";

    // ==================== 认证属性 ====================

    /// <summary>
    /// 是否需要认证
    /// </summary>
    public const string AuthRequired = "gateway.auth.required";

    /// <summary>
    /// 认证策略
    /// </summary>
    public const string AuthStrategy = "gateway.auth.strategy";

    /// <summary>
    /// 用户 ID
    /// </summary>
    public const string AuthUserId = "gateway.auth.user_id";

    /// <summary>
    /// 认证结果
    /// </summary>
    public const string AuthResult = "gateway.auth.result";

    /// <summary>
    /// 认证耗时（毫秒）
    /// </summary>
    public const string AuthDurationMs = "gateway.auth.duration_ms";

    // ==================== 限流属性 ====================

    /// <summary>
    /// 限流键
    /// </summary>
    public const string RateLimitKey = "gateway.ratelimit.key";

    /// <summary>
    /// 限流阈值
    /// </summary>
    public const string RateLimitLimit = "gateway.ratelimit.limit";

    /// <summary>
    /// 剩余配额
    /// </summary>
    public const string RateLimitRemaining = "gateway.ratelimit.remaining";

    /// <summary>
    /// 限流算法
    /// </summary>
    public const string RateLimitAlgorithm = "gateway.ratelimit.algorithm";

    /// <summary>
    /// 是否被限流
    /// </summary>
    public const string RateLimitRejected = "gateway.ratelimit.rejected";

    // ==================== 缓存属性 ====================

    /// <summary>
    /// 缓存键
    /// </summary>
    public const string CacheKey = "gateway.cache.key";

    /// <summary>
    /// 是否命中
    /// </summary>
    public const string CacheHit = "gateway.cache.hit";

    /// <summary>
    /// TTL（秒）
    /// </summary>
    public const string CacheTtlSeconds = "gateway.cache.ttl_seconds";

    /// <summary>
    /// 缓存操作类型（read/write）
    /// </summary>
    public const string CacheOperation = "gateway.cache.operation";

    // ==================== 转发属性 ====================

    /// <summary>
    /// 目标地址
    /// </summary>
    public const string ForwardingTarget = "gateway.forwarding.target";

    /// <summary>
    /// 目标服务名
    /// </summary>
    public const string ForwardingServiceName = "gateway.forwarding.service_name";

    /// <summary>
    /// 协议（http/https/grpc）
    /// </summary>
    public const string ForwardingProtocol = "gateway.forwarding.protocol";

    /// <summary>
    /// 转发耗时（毫秒）
    /// </summary>
    public const string ForwardingDurationMs = "gateway.forwarding.duration_ms";

    // ==================== HTTP 属性（遵循 OTel 语义约定）====================

    /// <summary>
    /// HTTP 方法
    /// </summary>
    public const string HttpMethod = "http.request.method";

    /// <summary>
    /// HTTP 状态码
    /// </summary>
    public const string HttpStatusCode = "http.response.status_code";

    /// <summary>
    /// URL 路径
    /// </summary>
    public const string UrlPath = "url.path";

    /// <summary>
    /// URL 查询字符串
    /// </summary>
    public const string UrlQuery = "url.query";

    /// <summary>
    /// 服务器地址
    /// </summary>
    public const string ServerAddress = "server.address";

    /// <summary>
    /// 服务器端口
    /// </summary>
    public const string ServerPort = "server.port";

    /// <summary>
    /// 客户端地址
    /// </summary>
    public const string ClientAddress = "client.address";

    /// <summary>
    /// 请求体大小（字节）
    /// </summary>
    public const string HttpRequestBodySize = "http.request.body.size";

    /// <summary>
    /// 响应体大小（字节）
    /// </summary>
    public const string HttpResponseBodySize = "http.response.body.size";

    // ==================== 弹性策略属性 ====================

    /// <summary>
    /// 重试次数
    /// </summary>
    public const string RetryAttempt = "gateway.retry.attempt";

    /// <summary>
    /// 重试延迟（毫秒）
    /// </summary>
    public const string RetryDelayMs = "gateway.retry.delay_ms";

    /// <summary>
    /// 重试原因
    /// </summary>
    public const string RetryReason = "gateway.retry.reason";

    /// <summary>
    /// 熔断器状态（closed/open/half-open）
    /// </summary>
    public const string CircuitBreakerState = "gateway.circuitbreaker.state";

    /// <summary>
    /// 熔断器名称
    /// </summary>
    public const string CircuitBreakerName = "gateway.circuitbreaker.name";

    /// <summary>
    /// 舱壁当前并发数
    /// </summary>
    public const string BulkheadConcurrency = "gateway.bulkhead.concurrency";

    /// <summary>
    /// 舱壁队列长度
    /// </summary>
    public const string BulkheadQueueLength = "gateway.bulkhead.queue_length";

    /// <summary>
    /// 舱壁名称
    /// </summary>
    public const string BulkheadName = "gateway.bulkhead.name";

    /// <summary>
    /// 是否被舱壁拒绝
    /// </summary>
    public const string BulkheadRejected = "gateway.bulkhead.rejected";

    /// <summary>
    /// 降级类型（static/cache/custom）
    /// </summary>
    public const string FallbackType = "gateway.fallback.type";

    /// <summary>
    /// 降级来源
    /// </summary>
    public const string FallbackSource = "gateway.fallback.source";

    /// <summary>
    /// 降级原因
    /// </summary>
    public const string FallbackReason = "gateway.fallback.reason";

    // ==================== 错误属性 ====================

    /// <summary>
    /// 错误类型
    /// </summary>
    public const string ErrorType = "error.type";

    /// <summary>
    /// 错误消息
    /// </summary>
    public const string ErrorMessage = "error.message";

    /// <summary>
    /// 错误堆栈
    /// </summary>
    public const string ErrorStackTrace = "error.stack_trace";
}
