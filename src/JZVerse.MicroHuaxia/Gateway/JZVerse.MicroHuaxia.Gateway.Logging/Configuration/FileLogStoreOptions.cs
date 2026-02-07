namespace JZVerse.MicroHuaxia.Gateway.Logging.Configuration;

/// <summary>
/// 文件日志存储配置选项
/// </summary>
public sealed class FileLogStoreOptions
{
    /// <summary>
    /// 是否启用文件存储
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 日志文件基础路径
    /// </summary>
    public string BasePath { get; set; } = "./logs/gateway";

    /// <summary>
    /// 文件名模式，支持占位符：
    /// - {Service}: 服务名称
    /// - {Level}: 日志级别
    /// - {Date}: 日期 (yyyy-MM-dd)
    /// - {Hour}: 小时 (HH)
    /// </summary>
    public string FileNamePattern { get; set; } = "{Service}/{Level}/log-{Date}.log";

    /// <summary>
    /// 是否使用 UTC 时间
    /// </summary>
    public bool UseUtcTime { get; set; } = false;

    /// <summary>
    /// 是否启用轮转
    /// </summary>
    public bool EnableRotation { get; set; } = true;

    /// <summary>
    /// 轮转策略
    /// </summary>
    public RotationStrategy RotationStrategy { get; set; } = RotationStrategy.Both;

    /// <summary>
    /// 单个文件最大大小（字节），默认 100MB
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 100 * 1024 * 1024;

    /// <summary>
    /// 时间轮转间隔（小时），默认 24 小时
    /// </summary>
    public int RotationIntervalHours { get; set; } = 24;

    /// <summary>
    /// 是否启用压缩
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// 压缩格式
    /// </summary>
    public CompressionFormat CompressionFormat { get; set; } = CompressionFormat.Gzip;

    /// <summary>
    /// 多少天后压缩文件
    /// </summary>
    public int CompressAfterDays { get; set; } = 1;

    /// <summary>
    /// 日志保留天数
    /// </summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>
    /// 最大总存储大小（字节），默认 10GB
    /// </summary>
    public long MaxTotalSizeBytes { get; set; } = 10L * 1024 * 1024 * 1024;

    /// <summary>
    /// 保留策略检查间隔（小时）
    /// </summary>
    public int RetentionCheckIntervalHours { get; set; } = 6;

    /// <summary>
    /// 写入缓冲区大小（字节），默认 64KB
    /// </summary>
    public int WriteBufferSize { get; set; } = 64 * 1024;

    /// <summary>
    /// 刷新间隔（秒）
    /// </summary>
    public int FlushIntervalSeconds { get; set; } = 1;
}

/// <summary>
/// 轮转策略
/// </summary>
public enum RotationStrategy
{
    /// <summary>
    /// 按文件大小轮转
    /// </summary>
    Size,

    /// <summary>
    /// 按时间轮转
    /// </summary>
    Time,

    /// <summary>
    /// 按大小和时间轮转（任一条件满足即轮转）
    /// </summary>
    Both
}

/// <summary>
/// 压缩格式
/// </summary>
public enum CompressionFormat
{
    /// <summary>
    /// 不压缩
    /// </summary>
    None,

    /// <summary>
    /// Gzip 压缩
    /// </summary>
    Gzip
}
