namespace JZVerse.MicroHuaxia.Dashboard.Abstractions.Models;

/// <summary>
/// Topic 基础信息
/// </summary>
public class MqTopicInfo
{
    public string Name { get; set; } = string.Empty;
    public int PartitionCount { get; set; }
    public long TotalMessages { get; set; }
    public int SubscriberCount { get; set; }
}

/// <summary>
/// Topic 详情
/// </summary>
public class MqTopicDetail
{
    public string Name { get; set; } = string.Empty;
    public List<MqPartitionInfo> Partitions { get; set; } = [];
    public List<string> Subscribers { get; set; } = [];
    public long TotalMessages => Partitions.Sum(p => p.MessageCount);
}

/// <summary>
/// 分区信息
/// </summary>
public class MqPartitionInfo
{
    public int PartitionId { get; set; }
    public long LatestOffset { get; set; }
    public long EarliestOffset { get; set; }
    public long MessageCount { get; set; }
}

/// <summary>
/// 消费者组信息
/// </summary>
public class MqConsumerGroupInfo
{
    public string GroupName { get; set; } = string.Empty;
    public List<string> SubscribedTopics { get; set; } = [];
    public long TotalLag { get; set; }
}

/// <summary>
/// 客户端连接信息
/// </summary>
public class MqClientConnectionInfo
{
    public string ClientId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; }
    public List<string> Topics { get; set; } = [];
}
