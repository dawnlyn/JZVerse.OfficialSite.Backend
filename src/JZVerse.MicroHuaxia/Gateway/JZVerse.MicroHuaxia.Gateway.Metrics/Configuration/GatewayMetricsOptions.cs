namespace JZVerse.MicroHuaxia.Gateway.Metrics.Configuration;

/// <summary>
/// 网关指标配置选项
/// </summary>
public sealed class GatewayMetricsOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Gateway:Metrics";

    /// <summary>
    /// 是否启用指标收集
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 是否收集请求指标
    /// </summary>
    public bool CollectRequestMetrics { get; set; } = true;

    /// <summary>
    /// 是否收集认证指标
    /// </summary>
    public bool CollectAuthMetrics { get; set; } = true;

    /// <summary>
    /// 是否收集限流指标
    /// </summary>
    public bool CollectRateLimitMetrics { get; set; } = true;

    /// <summary>
    /// 是否收集缓存指标
    /// </summary>
    public bool CollectCacheMetrics { get; set; } = true;

    /// <summary>
    /// 是否收集转发指标
    /// </summary>
    public bool CollectForwardMetrics { get; set; } = true;

    /// <summary>
    /// 是否收集弹性指标
    /// </summary>
    public bool CollectResilienceMetrics { get; set; } = true;

    /// <summary>
    /// 内存存储配置
    /// </summary>
    public InMemoryMetricsStoreOptions InMemoryStore { get; set; } = new();

    /// <summary>
    /// 是否启用 OTLP 导出器
    /// </summary>
    public bool EnableOtlpExporter { get; set; }

    /// <summary>
    /// OTLP 导出器端点
    /// </summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>
    /// OTLP 导出协议（Grpc 或 HttpProtobuf）
    /// </summary>
    public string OtlpProtocol { get; set; } = "Grpc";

    /// <summary>
    /// 是否启用 Prometheus 导出器
    /// </summary>
    public bool EnablePrometheusExporter { get; set; }

    /// <summary>
    /// Prometheus 端点路径
    /// </summary>
    public string PrometheusEndpoint { get; set; } = "/metrics";

    /// <summary>
    /// 是否启用控制台导出器（仅用于开发调试）
    /// </summary>
    public bool EnableConsoleExporter { get; set; }

    /// <summary>
    /// 是否启用 HTTP API
    /// </summary>
    public bool EnableHttpApi { get; set; } = true;

    /// <summary>
    /// 忽略的路径列表（不收集这些路径的指标）
    /// </summary>
    public List<string> IgnorePaths { get; set; } =
    [
        "/health",
        "/healthz",
        "/metrics",
        "/ready",
        "/live",
    ];

    /// <summary>
    /// 是否包含 ASP.NET Core 内置指标
    /// </summary>
    public bool IncludeAspNetCoreMetrics { get; set; } = true;

    /// <summary>
    /// 是否包含 HTTP 客户端指标
    /// </summary>
    public bool IncludeHttpClientMetrics { get; set; } = true;

    /// <summary>
    /// 是否包含运行时指标
    /// </summary>
    public bool IncludeRuntimeMetrics { get; set; } = true;

    /// <summary>
    /// 指标导出间隔（秒）
    /// </summary>
    public int ExportIntervalSeconds { get; set; } = 10;
}
