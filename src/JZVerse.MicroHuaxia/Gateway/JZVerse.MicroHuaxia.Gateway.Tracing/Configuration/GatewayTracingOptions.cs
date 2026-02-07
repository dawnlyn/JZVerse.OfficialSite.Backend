namespace JZVerse.MicroHuaxia.Gateway.Tracing.Configuration;

/// <summary>
/// 网关追踪配置选项
/// </summary>
public sealed class GatewayTracingOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Gateway:Tracing";

    /// <summary>
    /// 是否启用追踪（默认启用）
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 采样率（0.0-1.0，默认 1.0 全部采样）
    /// </summary>
    public double SamplingRatio { get; set; } = 1.0;

    /// <summary>
    /// 传播协议格式（默认 W3C）
    /// </summary>
    public TracePropagationFormat PropagationFormat { get; set; } = TracePropagationFormat.W3C;

    /// <summary>
    /// 自定义 Header 配置（仅当 PropagationFormat 为 Custom 时使用）
    /// </summary>
    public CustomHeaderOptions CustomHeaders { get; set; } = new();

    /// <summary>
    /// 详细记录配置
    /// </summary>
    public TraceRecordOptions Record { get; set; } = new();

    /// <summary>
    /// 内存存储配置
    /// </summary>
    public InMemoryStoreOptions InMemoryStore { get; set; } = new();

    /// <summary>
    /// 忽略追踪的路径列表（支持通配符）
    /// </summary>
    public List<string> IgnorePaths { get; set; } = ["/health", "/healthz", "/metrics", "/ready", "/live"];

    /// <summary>
    /// 始终采样错误请求（状态码 >= 400）
    /// </summary>
    public bool AlwaysSampleErrors { get; set; } = true;

    /// <summary>
    /// 始终采样慢请求（响应时间超过阈值）
    /// </summary>
    public bool AlwaysSampleSlowRequests { get; set; } = true;

    /// <summary>
    /// 慢请求阈值（毫秒，默认 5000ms）
    /// </summary>
    public int SlowRequestThresholdMs { get; set; } = 5000;
}

/// <summary>
/// 追踪传播协议格式
/// </summary>
public enum TracePropagationFormat
{
    /// <summary>
    /// W3C TraceContext（默认，推荐）
    /// 使用 traceparent 和 tracestate 头
    /// </summary>
    W3C,

    /// <summary>
    /// B3 单头模式
    /// 使用 b3 头
    /// </summary>
    B3Single,

    /// <summary>
    /// B3 多头模式
    /// 使用 X-B3-TraceId, X-B3-SpanId 等头
    /// </summary>
    B3Multi,

    /// <summary>
    /// 自定义 Header
    /// 使用 CustomHeaders 配置的自定义头
    /// </summary>
    Custom,

    /// <summary>
    /// 组合模式（同时支持多种协议）
    /// 提取时按 W3C -> B3Single -> B3Multi -> Custom 顺序尝试
    /// 注入时同时写入所有格式
    /// </summary>
    Composite,
}

/// <summary>
/// 自定义 Header 配置
/// </summary>
public sealed class CustomHeaderOptions
{
    /// <summary>
    /// Trace ID Header 名称（默认 X-Request-ID）
    /// </summary>
    public string TraceIdHeader { get; set; } = "X-Request-ID";

    /// <summary>
    /// Span ID Header 名称（默认 X-Span-ID）
    /// </summary>
    public string SpanIdHeader { get; set; } = "X-Span-ID";

    /// <summary>
    /// Parent Span ID Header 名称（可选）
    /// </summary>
    public string? ParentSpanIdHeader { get; set; } = "X-Parent-Span-ID";

    /// <summary>
    /// 采样标志 Header 名称（可选）
    /// </summary>
    public string? SampledHeader { get; set; } = "X-Sampled";
}

/// <summary>
/// 追踪详细记录配置
/// </summary>
public sealed class TraceRecordOptions
{
    /// <summary>
    /// 是否记录请求头
    /// </summary>
    public bool RecordRequestHeaders { get; set; }

    /// <summary>
    /// 是否记录响应头
    /// </summary>
    public bool RecordResponseHeaders { get; set; }

    /// <summary>
    /// 是否记录请求体（警告：可能包含敏感信息）
    /// </summary>
    public bool RecordRequestBody { get; set; }

    /// <summary>
    /// 是否记录响应体（警告：可能包含敏感信息）
    /// </summary>
    public bool RecordResponseBody { get; set; }

    /// <summary>
    /// 最大记录体大小（字节，默认 4KB）
    /// </summary>
    public int MaxBodySize { get; set; } = 4096;

    /// <summary>
    /// 敏感头列表（这些头不会被记录）
    /// </summary>
    public List<string> SensitiveHeaders { get; set; } =
    [
        "Authorization",
        "Cookie",
        "Set-Cookie",
        "X-Api-Key",
        "X-Auth-Token",
    ];
}

/// <summary>
/// 内存存储配置
/// </summary>
public sealed class InMemoryStoreOptions
{
    /// <summary>
    /// 是否启用内存存储（默认启用）
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 最大存储 Span 数量（默认 10000）
    /// </summary>
    public int MaxSpans { get; set; } = 10000;

    /// <summary>
    /// 保留时间（分钟，默认 30 分钟）
    /// </summary>
    public int RetentionMinutes { get; set; } = 30;

    /// <summary>
    /// 清理间隔（分钟，默认 5 分钟）
    /// </summary>
    public int CleanupIntervalMinutes { get; set; } = 5;
}
