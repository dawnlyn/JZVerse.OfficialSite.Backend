using JZVerse.MicroHuaxia.Gateway.Abstractions.TrafficControl;

namespace JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;

/// <summary>
/// 网关路由配置
/// </summary>
public sealed record GatewayRoute
{
    /// <summary>
    /// 路由唯一标识
    /// </summary>
    public required string RouteId { get; init; }

    /// <summary>
    /// 路由名称
    /// </summary>
    public required string RouteName { get; init; }

    /// <summary>
    /// 路由优先级（数字越小优先级越高）
    /// </summary>
    public int Priority { get; init; } = 100;

    /// <summary>
    /// 路由匹配规则
    /// </summary>
    public required RouteMatch Match { get; init; }

    /// <summary>
    /// 路由目标配置
    /// </summary>
    public required RouteDestination Destination { get; init; }

    /// <summary>
    /// 认证配置
    /// </summary>
    public RouteAuthentication? Authentication { get; init; }

    /// <summary>
    /// 限流配置
    /// </summary>
    public RouteRateLimit? RateLimit { get; init; }

    /// <summary>
    /// 缓存配置
    /// </summary>
    public RouteCache? Cache { get; init; }

    /// <summary>
    /// 请求超时时间
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// 重试配置
    /// </summary>
    public RouteRetry? Retry { get; init; }

    /// <summary>
    /// 熔断配置
    /// </summary>
    public RouteCircuitBreaker? CircuitBreaker { get; init; }

    /// <summary>
    /// 舱壁隔离配置
    /// </summary>
    public RouteBulkhead? Bulkhead { get; init; }

    /// <summary>
    /// 降级策略配置
    /// </summary>
    public RouteFallback? Fallback { get; init; }

    /// <summary>
    /// 流量染色配置
    /// </summary>
    public RouteTrafficColoring? TrafficColoring { get; init; }

    /// <summary>
    /// 流量镜像配置
    /// </summary>
    public RouteTrafficMirror? TrafficMirror { get; init; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// 元数据
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();
}

/// <summary>
/// 路由匹配规则
/// </summary>
public sealed record RouteMatch
{
    /// <summary>
    /// 路径模式（支持 {param} 和 {**catch-all}）
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// 允许的 HTTP 方法（为空表示允许所有方法）
    /// </summary>
    public List<string> Methods { get; init; } = [];

    /// <summary>
    /// Header 匹配规则
    /// </summary>
    public Dictionary<string, string> Headers { get; init; } = new();

    /// <summary>
    /// Query 参数匹配规则
    /// </summary>
    public Dictionary<string, string> QueryParams { get; init; } = new();

    /// <summary>
    /// 协议类型（http, https, ws, wss, grpc）
    /// </summary>
    public List<string> Schemes { get; init; } = [];

    /// <summary>
    /// 主机名匹配
    /// </summary>
    public List<string> Hosts { get; init; } = [];
}

/// <summary>
/// 路由目标配置
/// </summary>
public sealed record RouteDestination
{
    /// <summary>
    /// 目标服务名称（从服务发现解析）
    /// </summary>
    public string? ServiceName { get; init; }

    /// <summary>
    /// 直接指定的目标地址（不使用服务发现时）
    /// </summary>
    public string? DirectAddress { get; init; }

    /// <summary>
    /// 路径转换规则
    /// </summary>
    public string? PathTransform { get; init; }

    /// <summary>
    /// 负载均衡策略
    /// </summary>
    public string LoadBalancerStrategy { get; init; } = "RoundRobin";

    /// <summary>
    /// 是否要求健康实例
    /// </summary>
    public bool HealthCheckRequired { get; init; } = true;

    /// <summary>
    /// 优先版本
    /// </summary>
    public string? PreferredVersion { get; init; }

    /// <summary>
    /// 优先标签
    /// </summary>
    public HashSet<string> PreferredTags { get; init; } = [];
}

/// <summary>
/// 路由认证配置
/// </summary>
public sealed record RouteAuthentication
{
    /// <summary>
    /// 是否需要认证
    /// </summary>
    public bool Required { get; init; } = true;

    /// <summary>
    /// 认证策略名称列表
    /// </summary>
    public List<string> Strategies { get; init; } = [];

    /// <summary>
    /// 策略组合模式
    /// </summary>
    public AuthenticationCombineMode CombineMode { get; init; } = AuthenticationCombineMode.Or;
}

/// <summary>
/// 认证组合模式
/// </summary>
public enum AuthenticationCombineMode
{
    /// <summary>
    /// 任一策略通过即可
    /// </summary>
    Or,

    /// <summary>
    /// 所有策略必须通过
    /// </summary>
    And
}

/// <summary>
/// 路由限流配置
/// </summary>
public sealed record RouteRateLimit
{
    /// <summary>
    /// 是否启用限流
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// 限流算法
    /// </summary>
    public RateLimitAlgorithm Algorithm { get; init; } = RateLimitAlgorithm.SlidingWindow;

    /// <summary>
    /// 时间窗口内允许的请求数
    /// </summary>
    public int Limit { get; init; } = 100;

    /// <summary>
    /// 时间窗口
    /// </summary>
    public TimeSpan Window { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// 限流键生成策略
    /// </summary>
    public RateLimitKeyStrategy KeyStrategy { get; init; } = RateLimitKeyStrategy.ClientIp;

    /// <summary>
    /// 自定义限流键 Header 名称
    /// </summary>
    public string? CustomKeyHeader { get; init; }
}

/// <summary>
/// 限流算法
/// </summary>
public enum RateLimitAlgorithm
{
    /// <summary>
    /// 滑动窗口
    /// </summary>
    SlidingWindow,

    /// <summary>
    /// 令牌桶
    /// </summary>
    TokenBucket,

    /// <summary>
    /// 漏桶
    /// </summary>
    LeakyBucket
}

/// <summary>
/// 限流键策略
/// </summary>
public enum RateLimitKeyStrategy
{
    /// <summary>
    /// 基于客户端 IP
    /// </summary>
    ClientIp,

    /// <summary>
    /// 基于用户标识
    /// </summary>
    UserId,

    /// <summary>
    /// 基于路由
    /// </summary>
    Route,

    /// <summary>
    /// 全局限流
    /// </summary>
    Global,

    /// <summary>
    /// 自定义 Header
    /// </summary>
    CustomHeader
}

/// <summary>
/// 路由缓存配置
/// </summary>
public sealed record RouteCache
{
    /// <summary>
    /// 是否启用缓存
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// 缓存过期时间
    /// </summary>
    public TimeSpan Ttl { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 缓存键变化依据 Header
    /// </summary>
    public List<string> VaryByHeader { get; init; } = [];

    /// <summary>
    /// 缓存键变化依据 Query 参数
    /// </summary>
    public List<string> VaryByQuery { get; init; } = [];

    /// <summary>
    /// 允许缓存的状态码
    /// </summary>
    public List<int> CacheableStatusCodes { get; init; } = [200];
}

/// <summary>
/// 路由重试配置
/// </summary>
public sealed record RouteRetry
{
    /// <summary>
    /// 是否启用重试
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// 初始延迟
    /// </summary>
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// 最大延迟
    /// </summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 退避因子
    /// </summary>
    public double BackoffMultiplier { get; init; } = 2.0;

    /// <summary>
    /// 可重试的状态码
    /// </summary>
    public List<int> RetryableStatusCodes { get; init; } = [502, 503, 504];
}

/// <summary>
/// 路由熔断配置
/// </summary>
public sealed record RouteCircuitBreaker
{
    /// <summary>
    /// 是否启用熔断
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// 失败阈值
    /// </summary>
    public int FailureThreshold { get; init; } = 5;

    /// <summary>
    /// 恢复成功阈值
    /// </summary>
    public int SuccessThreshold { get; init; } = 2;

    /// <summary>
    /// 采样时间窗口
    /// </summary>
    public TimeSpan SamplingDuration { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 熔断持续时间
    /// </summary>
    public TimeSpan BreakDuration { get; init; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// 舱壁隔离配置
/// </summary>
public sealed record RouteBulkhead
{
    /// <summary>
    /// 是否启用舱壁隔离
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// 最大并发请求数
    /// </summary>
    public int MaxConcurrency { get; init; } = 100;

    /// <summary>
    /// 最大队列长度
    /// </summary>
    public int MaxQueueLength { get; init; } = 100;

    /// <summary>
    /// 队列超时时间
    /// </summary>
    public TimeSpan QueueTimeout { get; init; } = TimeSpan.FromSeconds(10);
}

/// <summary>
/// 降级策略配置
/// </summary>
public sealed record RouteFallback
{
    /// <summary>
    /// 是否启用降级
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// 降级类型
    /// </summary>
    public FallbackType Type { get; init; } = FallbackType.Static;

    /// <summary>
    /// 静态响应配置（当 Type = Static 时使用）
    /// </summary>
    public FallbackStaticResponse? StaticResponse { get; init; }

    /// <summary>
    /// 缓存降级配置（当 Type = Cache 时使用）
    /// </summary>
    public FallbackCacheOptions? CacheOptions { get; init; }

    /// <summary>
    /// 自定义处理器名称（当 Type = Custom 时使用）
    /// </summary>
    public string? CustomHandlerName { get; init; }
}

/// <summary>
/// 降级类型
/// </summary>
public enum FallbackType
{
    /// <summary>
    /// 返回预定义的静态响应
    /// </summary>
    Static,

    /// <summary>
    /// 返回缓存的最后成功响应
    /// </summary>
    Cache,

    /// <summary>
    /// 使用自定义处理器
    /// </summary>
    Custom
}

/// <summary>
/// 静态降级响应配置
/// </summary>
public sealed record FallbackStaticResponse
{
    /// <summary>
    /// HTTP 状态码
    /// </summary>
    public int StatusCode { get; init; } = 503;

    /// <summary>
    /// 响应体内容
    /// </summary>
    public string Body { get; init; } = "{\"error\":\"ServiceUnavailable\",\"message\":\"服务暂时不可用\"}";

    /// <summary>
    /// 内容类型
    /// </summary>
    public string ContentType { get; init; } = "application/json";

    /// <summary>
    /// 额外的响应头
    /// </summary>
    public Dictionary<string, string> Headers { get; init; } = new();
}

/// <summary>
/// 缓存降级配置
/// </summary>
public sealed record FallbackCacheOptions
{
    /// <summary>
    /// 降级缓存的最大有效期（可以比正常缓存更长）
    /// </summary>
    public TimeSpan MaxAge { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// 如果缓存也不存在，是否使用默认响应
    /// </summary>
    public bool UseDefaultIfNotCached { get; init; } = true;

    /// <summary>
    /// 缓存不存在时的默认响应
    /// </summary>
    public FallbackStaticResponse? DefaultResponse { get; init; }
}
