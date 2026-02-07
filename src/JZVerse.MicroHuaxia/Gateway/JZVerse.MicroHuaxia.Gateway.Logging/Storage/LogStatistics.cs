using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Gateway.Logging.Storage;

/// <summary>
/// 日志统计信息
/// </summary>
public sealed class LogStatistics
{
    /// <summary>
    /// 总日志条数
    /// </summary>
    public long TotalCount { get; init; }

    /// <summary>
    /// 按级别统计
    /// </summary>
    public IReadOnlyDictionary<LogLevel, long> CountByLevel { get; init; } = new Dictionary<LogLevel, long>();

    /// <summary>
    /// 按服务统计
    /// </summary>
    public IReadOnlyDictionary<string, long> CountByService { get; init; } = new Dictionary<string, long>();

    /// <summary>
    /// 按来源统计
    /// </summary>
    public IReadOnlyDictionary<LogSource, long> CountBySource { get; init; } = new Dictionary<LogSource, long>();

    /// <summary>
    /// 最早日志时间
    /// </summary>
    public DateTimeOffset? EarliestTimestamp { get; init; }

    /// <summary>
    /// 最新日志时间
    /// </summary>
    public DateTimeOffset? LatestTimestamp { get; init; }

    /// <summary>
    /// 错误率（Error + Critical / Total）
    /// </summary>
    public double ErrorRate { get; init; }

    /// <summary>
    /// 每分钟日志数（最近一小时平均）
    /// </summary>
    public double LogsPerMinute { get; init; }

    /// <summary>
    /// 存储使用情况
    /// </summary>
    public StorageStatistics? Storage { get; init; }

    /// <summary>
    /// 统计时间
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// 存储统计信息
/// </summary>
public sealed class StorageStatistics
{
    /// <summary>
    /// 存储类型
    /// </summary>
    public string StorageType { get; init; } = string.Empty;

    /// <summary>
    /// 已使用空间（字节）
    /// </summary>
    public long UsedBytes { get; init; }

    /// <summary>
    /// 最大空间限制（字节）
    /// </summary>
    public long MaxBytes { get; init; }

    /// <summary>
    /// 使用率（0-1）
    /// </summary>
    public double UsageRate => MaxBytes > 0 ? (double)UsedBytes / MaxBytes : 0;

    /// <summary>
    /// 文件数量（仅文件存储）
    /// </summary>
    public int? FileCount { get; init; }

    /// <summary>
    /// 压缩文件数量（仅文件存储）
    /// </summary>
    public int? CompressedFileCount { get; init; }

    /// <summary>
    /// 分区数量（仅 SQLite 存储）
    /// </summary>
    public int? PartitionCount { get; init; }
}
