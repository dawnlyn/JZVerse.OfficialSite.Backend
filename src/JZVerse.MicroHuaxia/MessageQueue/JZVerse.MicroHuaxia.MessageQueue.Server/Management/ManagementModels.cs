namespace JZVerse.MicroHuaxia.MessageQueue.Server.Management;

/// <summary>
/// Broker 统计信息
/// </summary>
public sealed class BrokerStats
{
    public int TotalTopics { get; set; }
    public long TotalMessages { get; set; }
    public int ConnectedClients { get; set; }
    public long PendingMessages { get; set; }
    public double MessagesPerSecond { get; set; }
}

/// <summary>
/// Topic 基础信息
/// </summary>
public sealed class TopicInfo
{
    public string Name { get; set; } = string.Empty;
    public int PartitionCount { get; set; }
    public long TotalMessages { get; set; }
    public int SubscriberCount { get; set; }
}

/// <summary>
/// Topic 详情
/// </summary>
public sealed class TopicDetail
{
    public string Name { get; set; } = string.Empty;
    public List<PartitionInfo> Partitions { get; set; } = [];
    public List<string> Subscribers { get; set; } = [];
}

/// <summary>
/// 分区信息
/// </summary>
public sealed class PartitionInfo
{
    public int PartitionId { get; set; }
    public long LatestOffset { get; set; }
    public long EarliestOffset { get; set; }
    public long MessageCount { get; set; }
}

/// <summary>
/// 消费者组信息
/// </summary>
public sealed class ConsumerGroupInfo
{
    public string GroupName { get; set; } = string.Empty;
    public List<string> SubscribedTopics { get; set; } = [];
    public long TotalLag { get; set; }
}

/// <summary>
/// 客户端连接信息
/// </summary>
public sealed class ClientConnectionInfo
{
    public string ClientId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public DateTimeOffset ConnectedAt { get; set; }
    public List<string> Topics { get; set; } = [];
}
