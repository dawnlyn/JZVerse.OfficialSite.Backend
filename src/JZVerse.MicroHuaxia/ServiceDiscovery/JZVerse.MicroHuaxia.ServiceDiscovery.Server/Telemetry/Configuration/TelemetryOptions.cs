namespace JZVerse.MicroHuaxia.ServiceDiscovery.Server.Telemetry.Configuration;

/// <summary>
/// 可观测性配置选项
/// </summary>
public sealed class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    /// <summary>
    /// 是否启用可观测性 (默认开发环境启用)
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 服务名称 (用于标识遥测数据来源)
    /// </summary>
    public string ServiceName { get; set; } = "ServiceDiscoveryServer";

    /// <summary>
    /// 服务版本
    /// </summary>
    public string ServiceVersion { get; set; } = "1.0.0";

    /// <summary>
    /// 日志配置
    /// </summary>
    public LoggingOptions Logging { get; set; } = new();

    /// <summary>
    /// 追踪配置
    /// </summary>
    public TracingOptions Tracing { get; set; } = new();

    /// <summary>
    /// 指标配置
    /// </summary>
    public MetricsOptions Metrics { get; set; } = new();

    /// <summary>
    /// OTLP 导出器配置
    /// </summary>
    public OtlpExporterOptions? OtlpExporter { get; set; }

    /// <summary>
    /// Console 导出器配置 (开发调试用)
    /// </summary>
    public ConsoleExporterOptions Console { get; set; } = new();
}

/// <summary>
/// 日志配置
/// </summary>
public sealed class LoggingOptions
{
    /// <summary>
    /// 是否启用日志聚合
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 最小日志级别
    /// </summary>
    public string MinimumLevel { get; set; } = "Information";

    /// <summary>
    /// Dashboard 内存存储最大条数
    /// </summary>
    public int MaxInMemoryLogs { get; set; } = 10000;

    /// <summary>
    /// 是否包含 Scope 信息
    /// </summary>
    public bool IncludeScopes { get; set; } = true;

    /// <summary>
    /// 是否包含异常堆栈
    /// </summary>
    public bool IncludeStackTrace { get; set; } = true;
}

/// <summary>
/// 追踪配置
/// </summary>
public sealed class TracingOptions
{
    /// <summary>
    /// 是否启用分布式追踪
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 采样率 (0.0-1.0)
    /// </summary>
    public double SamplingRatio { get; set; } = 1.0;

    /// <summary>
    /// 是否记录 HTTP 请求追踪
    /// </summary>
    public bool RecordHttpRequests { get; set; } = true;

    /// <summary>
    /// 是否记录服务注册操作追踪
    /// </summary>
    public bool RecordRegistrationOperations { get; set; } = true;

    /// <summary>
    /// 是否记录健康检查追踪
    /// </summary>
    public bool RecordHealthChecks { get; set; } = true;
}

/// <summary>
/// 指标配置
/// </summary>
public sealed class MetricsOptions
{
    /// <summary>
    /// 是否启用指标收集
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 指标收集间隔 (秒)
    /// </summary>
    public int CollectionIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// 是否导出 ASP.NET Core 指标
    /// </summary>
    public bool IncludeAspNetCoreMetrics { get; set; } = true;

    /// <summary>
    /// 是否导出运行时指标
    /// </summary>
    public bool IncludeRuntimeMetrics { get; set; } = true;
}

/// <summary>
/// OTLP 导出器配置
/// </summary>
public sealed class OtlpExporterOptions
{
    /// <summary>
    /// OTLP 端点
    /// </summary>
    public string Endpoint { get; set; } = "http://localhost:4317";

    /// <summary>
    /// 协议 (Grpc/HttpProtobuf)
    /// </summary>
    public string Protocol { get; set; } = "Grpc";

    /// <summary>
    /// 请求头 (认证等)
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = [];

    /// <summary>
    /// 超时时间 (秒)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;
}

/// <summary>
/// Console 导出器配置
/// </summary>
public sealed class ConsoleExporterOptions
{
    /// <summary>
    /// 是否启用控制台输出 (仅开发环境)
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 是否输出日志
    /// </summary>
    public bool Logs { get; set; }

    /// <summary>
    /// 是否输出追踪
    /// </summary>
    public bool Traces { get; set; }

    /// <summary>
    /// 是否输出指标
    /// </summary>
    public bool Metrics { get; set; }
}
