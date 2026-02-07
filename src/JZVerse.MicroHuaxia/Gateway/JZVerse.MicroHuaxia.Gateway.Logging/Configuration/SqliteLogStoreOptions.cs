namespace JZVerse.MicroHuaxia.Gateway.Logging.Configuration;

/// <summary>
/// SQLite 日志存储配置选项
/// </summary>
public sealed class SqliteLogStoreOptions
{
    /// <summary>
    /// 是否启用 SQLite 存储
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 数据库文件路径
    /// </summary>
    public string DatabasePath { get; set; } = "./logs/gateway.db";

    /// <summary>
    /// 是否启用分区（按时间分表）
    /// </summary>
    public bool EnablePartitioning { get; set; } = true;

    /// <summary>
    /// 分区策略
    /// </summary>
    public PartitionStrategy PartitionStrategy { get; set; } = PartitionStrategy.Daily;

    /// <summary>
    /// 是否创建索引
    /// </summary>
    public bool CreateIndexes { get; set; } = true;

    /// <summary>
    /// 索引列
    /// </summary>
    public List<string> IndexedColumns { get; set; } =
    [
        "TraceId",
        "Timestamp",
        "Level",
        "ServiceName",
        "RequestPath"
    ];

    /// <summary>
    /// 是否启用全文搜索（FTS5）
    /// </summary>
    public bool EnableFullTextSearch { get; set; } = true;

    /// <summary>
    /// 连接池大小
    /// </summary>
    public int MaxConnectionPoolSize { get; set; } = 10;

    /// <summary>
    /// 是否启用 WAL 模式（提升并发性能）
    /// </summary>
    public bool EnableWal { get; set; } = true;

    /// <summary>
    /// 批量插入大小
    /// </summary>
    public int BatchInsertSize { get; set; } = 100;

    /// <summary>
    /// 批量插入刷新间隔（秒）
    /// </summary>
    public int BatchFlushIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// VACUUM 执行间隔（天）
    /// </summary>
    public int VacuumIntervalDays { get; set; } = 7;

    /// <summary>
    /// 分区保留天数
    /// </summary>
    public int PartitionRetentionDays { get; set; } = 30;
}

/// <summary>
/// 分区策略
/// </summary>
public enum PartitionStrategy
{
    /// <summary>
    /// 按天分区
    /// </summary>
    Daily,

    /// <summary>
    /// 按月分区
    /// </summary>
    Monthly
}
