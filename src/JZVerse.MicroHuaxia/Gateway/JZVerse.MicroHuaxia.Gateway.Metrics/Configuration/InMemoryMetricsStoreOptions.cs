namespace JZVerse.MicroHuaxia.Gateway.Metrics.Configuration;

/// <summary>
/// 内存指标存储配置选项
/// </summary>
public sealed class InMemoryMetricsStoreOptions
{
    /// <summary>
    /// 是否启用内存存储
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 最大数据点数量
    /// </summary>
    public int MaxDataPoints { get; set; } = 100_000;

    /// <summary>
    /// 数据保留时间（分钟）
    /// </summary>
    public int RetentionMinutes { get; set; } = 60;

    /// <summary>
    /// 分桶大小（秒）
    /// </summary>
    public int BucketSizeSeconds { get; set; } = 10;

    /// <summary>
    /// 清理间隔（分钟）
    /// </summary>
    public int CleanupIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// 是否启用聚合
    /// </summary>
    public bool EnableAggregation { get; set; } = true;

    /// <summary>
    /// 聚合窗口大小（秒）
    /// </summary>
    public int AggregationWindowSeconds { get; set; } = 60;
}
