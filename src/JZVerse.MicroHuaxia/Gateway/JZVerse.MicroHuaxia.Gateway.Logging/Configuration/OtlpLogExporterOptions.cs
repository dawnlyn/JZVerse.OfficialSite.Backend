namespace JZVerse.MicroHuaxia.Gateway.Logging.Configuration;

/// <summary>
/// OTLP 日志导出器配置选项
/// </summary>
public sealed class OtlpLogExporterOptions
{
    /// <summary>
    /// OTLP 端点地址
    /// </summary>
    public string Endpoint { get; set; } = "http://localhost:4317";

    /// <summary>
    /// 协议类型
    /// </summary>
    public OtlpProtocol Protocol { get; set; } = OtlpProtocol.Grpc;

    /// <summary>
    /// 自定义 HTTP 头部（用于认证等）
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = [];

    /// <summary>
    /// 批量导出大小
    /// </summary>
    public int BatchSize { get; set; } = 512;

    /// <summary>
    /// 导出间隔（秒）
    /// </summary>
    public int ExportIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// 导出超时（秒）
    /// </summary>
    public int ExportTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 最大队列大小
    /// </summary>
    public int MaxQueueSize { get; set; } = 2048;

    /// <summary>
    /// 是否启用重试
    /// </summary>
    public bool EnableRetry { get; set; } = true;

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;
}

/// <summary>
/// OTLP 协议类型
/// </summary>
public enum OtlpProtocol
{
    /// <summary>
    /// gRPC 协议
    /// </summary>
    Grpc,

    /// <summary>
    /// HTTP/Protobuf 协议
    /// </summary>
    HttpProtobuf
}
