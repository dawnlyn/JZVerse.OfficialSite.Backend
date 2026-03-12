namespace JZVerse.MicroHuaxia.ServiceDiscovery.Server.Telemetry.Tracing;

/// <summary>
/// Span 属性常量 (遵循 OpenTelemetry 语义约定)
/// </summary>
public static class SpanAttributes
{
    // 服务相关
    public const string ServiceName = "service.name";
    public const string ServiceVersion = "service.version";
    public const string InstanceId = "service.instance.id";

    // 操作相关
    public const string Operation = "sd.operation";
    public const string OperationResult = "sd.operation.result";

    // 健康检查相关
    public const string HealthStatus = "sd.health.status";
    public const string HealthCheckType = "sd.health.check.type";
    public const string HealthCheckDuration = "sd.health.check.duration_ms";
    public const string HealthCheckEndpoint = "sd.health.check.endpoint";

    // 注册相关
    public const string RegistrationHost = "sd.registration.host";
    public const string RegistrationPort = "sd.registration.port";
    public const string RegistrationScheme = "sd.registration.scheme";
    public const string RegistrationTags = "sd.registration.tags";

    // 发现相关
    public const string DiscoveryInstanceCount = "sd.discovery.instance_count";
    public const string DiscoveryHealthyCount = "sd.discovery.healthy_count";
    public const string DiscoveryLoadBalancer = "sd.discovery.load_balancer";
    public const string DiscoveryCacheHit = "sd.discovery.cache_hit";

    // 错误相关
    public const string ErrorType = "error.type";
    public const string ErrorMessage = "error.message";
    public const string ErrorStackTrace = "error.stack_trace";

    // 缓存相关
    public const string CacheHit = "sd.cache.hit";
    public const string CacheTtl = "sd.cache.ttl_seconds";

    // 心跳相关
    public const string HeartbeatPreviousStatus = "sd.heartbeat.previous_status";
    public const string HeartbeatNewStatus = "sd.heartbeat.new_status";
}
