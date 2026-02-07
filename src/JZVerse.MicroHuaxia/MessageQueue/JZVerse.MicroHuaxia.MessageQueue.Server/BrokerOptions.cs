namespace JZVerse.MicroHuaxia.MessageQueue.Server;

/// <summary>
/// Broker 服务器配置选项
/// </summary>
public sealed class BrokerOptions
{
    /// <summary>监听地址</summary>
    public string Host { get; set; } = "0.0.0.0";

    /// <summary>监听端口</summary>
    public int Port { get; set; } = 9527;

    /// <summary>存储类型</summary>
    public StorageType StorageType { get; set; } = StorageType.Memory;

    /// <summary>数据目录 (FileLog 存储使用)</summary>
    public string DataDirectory { get; set; } = "./data";

    /// <summary>最大连接数</summary>
    public int MaxConnections { get; set; } = 10000;

    /// <summary>心跳超时时间 (秒)</summary>
    public int HeartbeatTimeout { get; set; } = 60;

    /// <summary>消息保留时间 (小时)</summary>
    public int MessageRetentionHours { get; set; } = 168; // 7 days

    /// <summary>默认分区数</summary>
    public int DefaultPartitions { get; set; } = 4;

    /// <summary>启用消息追踪</summary>
    public bool EnableTracing { get; set; } = false;

    /// <summary>启用消息压缩</summary>
    public bool EnableCompression { get; set; } = false;

    /// <summary>日志级别</summary>
    public string LogLevel { get; set; } = "Information";
}

/// <summary>
/// 存储类型
/// </summary>
public enum StorageType
{
    /// <summary>内存存储</summary>
    Memory,
    /// <summary>文件日志存储</summary>
    FileLog,
    /// <summary>混合存储 (内存 + 文件)</summary>
    Hybrid
}
