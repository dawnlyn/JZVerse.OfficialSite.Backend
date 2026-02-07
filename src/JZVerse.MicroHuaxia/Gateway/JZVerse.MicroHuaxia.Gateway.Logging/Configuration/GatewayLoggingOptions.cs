using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Gateway.Logging.Configuration;

/// <summary>
/// Gateway 日志系统的主配置选项
/// </summary>
public sealed class GatewayLoggingOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Gateway:Logging";

    /// <summary>
    /// 是否启用日志系统
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 最小日志级别
    /// </summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// 是否包含 Scope 信息
    /// </summary>
    public bool IncludeScopes { get; set; } = true;

    /// <summary>
    /// 是否包含异常堆栈跟踪
    /// </summary>
    public bool IncludeStackTrace { get; set; } = true;

    /// <summary>
    /// 是否自动注入 TraceId/SpanId（从 Activity.Current）
    /// </summary>
    public bool EnrichWithTraceContext { get; set; } = true;

    /// <summary>
    /// 忽略的请求路径（不记录日志）
    /// </summary>
    public List<string> IgnorePaths { get; set; } =
    [
        "/health",
        "/healthz",
        "/ready",
        "/live",
        "/metrics"
    ];

    /// <summary>
    /// 分类策略
    /// </summary>
    public ClassificationStrategy ClassificationStrategy { get; set; } = ClassificationStrategy.Composite;

    /// <summary>
    /// 时间分类粒度（当分类策略包含时间维度时）
    /// </summary>
    public TimeClassificationGranularity TimeClassificationGranularity { get; set; } = TimeClassificationGranularity.Day;

    /// <summary>
    /// 内存存储配置
    /// </summary>
    public InMemoryLogStoreOptions InMemoryStore { get; set; } = new();

    /// <summary>
    /// 文件存储配置
    /// </summary>
    public FileLogStoreOptions FileStore { get; set; } = new();

    /// <summary>
    /// SQLite 存储配置
    /// </summary>
    public SqliteLogStoreOptions SqliteStore { get; set; } = new();

    /// <summary>
    /// 是否启用 OTLP 导出
    /// </summary>
    public bool EnableOtlpExporter { get; set; }

    /// <summary>
    /// OTLP 导出器配置
    /// </summary>
    public OtlpLogExporterOptions OtlpExporter { get; set; } = new();

    /// <summary>
    /// 是否启用 HTTP 查询 API
    /// </summary>
    public bool EnableHttpApi { get; set; } = true;

    /// <summary>
    /// 是否启用 WebSocket 实时流
    /// </summary>
    public bool EnableWebSocketStream { get; set; } = true;
}

/// <summary>
/// 日志分类策略
/// </summary>
public enum ClassificationStrategy
{
    /// <summary>
    /// 按服务/组件分类
    /// </summary>
    Service,

    /// <summary>
    /// 按日志级别分类
    /// </summary>
    Level,

    /// <summary>
    /// 按时间分类
    /// </summary>
    Time,

    /// <summary>
    /// 混合分类（服务 + 级别 + 时间）
    /// </summary>
    Composite
}

/// <summary>
/// 时间分类粒度
/// </summary>
public enum TimeClassificationGranularity
{
    /// <summary>
    /// 按小时分类
    /// </summary>
    Hour,

    /// <summary>
    /// 按天分类
    /// </summary>
    Day
}
