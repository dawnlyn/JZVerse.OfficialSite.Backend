namespace JZVerse.MicroHuaxia.MessageQueue.Storage.FileLog;

/// <summary>
/// FileLog 存储选项
/// </summary>
public sealed class FileLogStoreOptions
{
    /// <summary>
    /// 数据目录
    /// </summary>
    public string DataDirectory { get; set; } = "./data/messagequeue";

    /// <summary>
    /// 日志段大小（字节），默认 1GB
    /// </summary>
    public long SegmentSizeBytes { get; set; } = 1024L * 1024 * 1024;

    /// <summary>
    /// 索引间隔（字节），每隔多少字节创建一个索引条目
    /// </summary>
    public int IndexIntervalBytes { get; set; } = 4096;

    /// <summary>
    /// 消息保留时间
    /// </summary>
    public TimeSpan MessageRetention { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// 是否启用 WAL（Write-Ahead Log）
    /// </summary>
    public bool EnableWal { get; set; } = true;

    /// <summary>
    /// WAL 刷盘策略
    /// </summary>
    public FlushPolicy FlushPolicy { get; set; } = FlushPolicy.EverySecond;

    /// <summary>
    /// 是否启用消息压缩
    /// </summary>
    public bool EnableCompression { get; set; } = false;

    /// <summary>
    /// 压缩算法
    /// </summary>
    public CompressionType CompressionType { get; set; } = CompressionType.None;
}

/// <summary>
/// 刷盘策略
/// </summary>
public enum FlushPolicy
{
    /// <summary>
    /// 每条消息刷盘
    /// </summary>
    EveryMessage = 0,

    /// <summary>
    /// 每秒刷盘
    /// </summary>
    EverySecond = 1,

    /// <summary>
    /// 由操作系统决定
    /// </summary>
    OsDefault = 2
}

/// <summary>
/// 压缩类型
/// </summary>
public enum CompressionType
{
    /// <summary>
    /// 不压缩
    /// </summary>
    None = 0,

    /// <summary>
    /// LZ4 压缩
    /// </summary>
    Lz4 = 1,

    /// <summary>
    /// Zstd 压缩
    /// </summary>
    Zstd = 2,

    /// <summary>
    /// GZip 压缩
    /// </summary>
    Gzip = 3
}
