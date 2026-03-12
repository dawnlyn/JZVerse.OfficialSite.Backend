namespace JZVerse.MicroHuaxia.Observability.Core.Configuration;

/// <summary>
/// 可观测性统一配置
/// </summary>
public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    /// <summary>
    /// 服务名称
    /// </summary>
    public string ServiceName { get; set; } = "UnknownService";

    /// <summary>
    /// 服务版本
    /// </summary>
    public string ServiceVersion { get; set; } = "1.0.0";

    /// <summary>
    /// 控制台输出配置
    /// </summary>
    public ConsoleOptions Console { get; set; } = new();

    /// <summary>
    /// 文件日志配置
    /// </summary>
    public FileOptions File { get; set; } = new();

    /// <summary>
    /// OpenTelemetry 配置
    /// </summary>
    public OpenTelemetryOptions OpenTelemetry { get; set; } = new();

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
}

/// <summary>
/// 控制台输出配置
/// </summary>
public sealed class ConsoleOptions
{
    /// <summary>
    /// 是否启用控制台输出
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 是否启用 ANSI 颜色输出
    /// </summary>
    public bool EnableColors { get; set; } = true;

    /// <summary>
    /// 是否记录请求体
    /// </summary>
    public bool LogRequestBody { get; set; } = true;

    /// <summary>
    /// 是否记录响应体
    /// </summary>
    public bool LogResponseBody { get; set; } = true;

    /// <summary>
    /// 是否记录请求头
    /// </summary>
    public bool LogRequestHeaders { get; set; } = false;

    /// <summary>
    /// JSON 载荷最大显示长度（超出部分截断）
    /// </summary>
    public int MaxPayloadLength { get; set; } = 2048;

    /// <summary>
    /// 慢调用阈值（毫秒），超过此值会用醒目颜色标记
    /// </summary>
    public int SlowCallThresholdMs { get; set; } = 500;

    /// <summary>
    /// 敏感字段名集合，匹配到的字段值会被脱敏为 ***
    /// </summary>
    public HashSet<string> SensitiveFields { get; set; } =
        ["password", "secret", "token", "apiKey", "connectionString"];

    /// <summary>
    /// 排除的路径模式，匹配到的请求不记录日志
    /// </summary>
    public HashSet<string> ExcludedPaths { get; set; } = ["/health", "/healthz"];
}

/// <summary>
/// 文件日志格式
/// </summary>
public enum FileLogFormat
{
    /// <summary>
    /// 纯文本（去色彩的控制台格式）
    /// </summary>
    Text,

    /// <summary>
    /// JSON 格式（每行一个 JSON 对象）
    /// </summary>
    Json
}

/// <summary>
/// 文件日志配置
/// </summary>
public sealed class FileOptions
{
    /// <summary>
    /// 是否启用文件日志
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 日志文件目录（相对或绝对路径）
    /// </summary>
    public string Directory { get; set; } = "logs";

    /// <summary>
    /// 文件名模式，支持 {ServiceName} 和 {Date} 占位符
    /// </summary>
    public string FileNamePattern { get; set; } = "{ServiceName}-{Date}.log";

    /// <summary>
    /// 输出格式
    /// </summary>
    public FileLogFormat Format { get; set; } = FileLogFormat.Text;

    /// <summary>
    /// 单文件最大大小（MB），超过则轮换
    /// </summary>
    public int MaxFileSizeMB { get; set; } = 50;

    /// <summary>
    /// 保留天数，超过自动删除
    /// </summary>
    public int RetainDays { get; set; } = 30;

    /// <summary>
    /// 所有日志文件总大小上限（MB）
    /// </summary>
    public int MaxTotalSizeMB { get; set; } = 1024;

    /// <summary>
    /// 异步写入刷盘间隔（秒）
    /// </summary>
    public int FlushIntervalSeconds { get; set; } = 1;
}

/// <summary>
/// OpenTelemetry 配置
/// </summary>
public sealed class OpenTelemetryOptions
{
    /// <summary>
    /// 是否启用 OpenTelemetry
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// OTLP 端点
    /// </summary>
    public string Endpoint { get; set; } = "http://localhost:17011";

    /// <summary>
    /// 协议 (Grpc / HttpProtobuf)
    /// </summary>
    public string Protocol { get; set; } = "Grpc";

    /// <summary>
    /// 请求头
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = [];

    /// <summary>
    /// 超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;
}

/// <summary>
/// 日志配置
/// </summary>
public sealed class LoggingOptions
{
    /// <summary>
    /// 是否启用日志
    /// </summary>
    public bool Enabled { get; set; } = true;
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
    /// 指标收集间隔（秒）
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
