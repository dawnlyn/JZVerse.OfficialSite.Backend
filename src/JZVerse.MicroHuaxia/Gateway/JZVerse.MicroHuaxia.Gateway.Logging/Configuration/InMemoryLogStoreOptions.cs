namespace JZVerse.MicroHuaxia.Gateway.Logging.Configuration;

/// <summary>
/// 内存日志存储配置选项
/// </summary>
public sealed class InMemoryLogStoreOptions
{
    /// <summary>
    /// 是否启用内存存储
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 最大日志条目数量（达到后自动淘汰最旧的）
    /// </summary>
    public int MaxCapacity { get; set; } = 50_000;

    /// <summary>
    /// 是否启用索引（加速 TraceId、RequestId 等查询）
    /// </summary>
    public bool EnableIndexing { get; set; } = true;

    /// <summary>
    /// 索引字段列表
    /// </summary>
    public List<string> IndexedFields { get; set; } =
    [
        "TraceId",
        "RequestId",
        "ServiceName"
    ];

    /// <summary>
    /// 日志保留时间（分钟），超过此时间的日志将被清理
    /// 设置为 0 表示不按时间清理，仅按容量清理
    /// </summary>
    public int RetentionMinutes { get; set; } = 60;

    /// <summary>
    /// 清理检查间隔（分钟）
    /// </summary>
    public int CleanupIntervalMinutes { get; set; } = 5;
}
