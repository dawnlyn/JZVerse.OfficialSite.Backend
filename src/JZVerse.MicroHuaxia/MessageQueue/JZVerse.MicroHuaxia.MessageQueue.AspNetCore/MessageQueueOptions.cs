using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Protocol;

namespace JZVerse.MicroHuaxia.MessageQueue.AspNetCore;

/// <summary>
/// 消息队列选项
/// </summary>
public sealed class MessageQueueOptions
{
    /// <summary>
    /// 消息队列模式
    /// </summary>
    public MessageQueueMode Mode { get; set; } = MessageQueueMode.InProc;

    /// <summary>
    /// Broker 端点列表（Broker 模式时使用）
    /// </summary>
    public string[] BrokerEndpoints { get; set; } = ["localhost:9092"];

    /// <summary>
    /// 协议类型
    /// </summary>
    public ProtocolType Protocol { get; set; } = ProtocolType.InProc;

    /// <summary>
    /// 存储类型
    /// </summary>
    public StorageType Storage { get; set; } = StorageType.Memory;

    /// <summary>
    /// 是否启用事务消息
    /// </summary>
    public bool EnableTransactional { get; set; } = false;

    /// <summary>
    /// 是否启用消息追踪
    /// </summary>
    public bool EnableTracing { get; set; } = false;

    /// <summary>
    /// 是否启用延迟消息
    /// </summary>
    public bool EnableDelayMessage { get; set; } = false;

    /// <summary>
    /// 每个分区最大消息数（内存存储）
    /// </summary>
    public int MaxMessagesPerPartition { get; set; } = 100_000;

    /// <summary>
    /// 数据目录（FileLog 存储）
    /// </summary>
    public string DataDirectory { get; set; } = "./data/messagequeue";
}

/// <summary>
/// 消息队列模式
/// </summary>
public enum MessageQueueMode
{
    /// <summary>
    /// 进程内模式（无需 Broker）
    /// </summary>
    InProc = 0,

    /// <summary>
    /// Broker 模式（需要独立的 Broker 服务）
    /// </summary>
    Broker = 1,

    /// <summary>
    /// Brokerless 模式（点对点直连）
    /// </summary>
    Brokerless = 2
}

/// <summary>
/// 存储类型
/// </summary>
public enum StorageType
{
    /// <summary>
    /// 内存存储
    /// </summary>
    Memory = 0,

    /// <summary>
    /// 文件日志存储
    /// </summary>
    FileLog = 1,

    /// <summary>
    /// 混合存储（内存 + 文件日志）
    /// </summary>
    Hybrid = 2
}
