namespace JZVerse.MicroHuaxia.Dashboard.Abstractions.Models;

/// <summary>
/// Dashboard 总览数据
/// </summary>
public class DashboardOverview
{
    /// <summary>
    /// 服务发现统计
    /// </summary>
    public ServiceDiscoveryStats ServiceDiscovery { get; set; } = new();

    /// <summary>
    /// 配置中心统计
    /// </summary>
    public ConfigCenterStats ConfigCenter { get; set; } = new();

    /// <summary>
    /// 网关统计
    /// </summary>
    public GatewayStats Gateway { get; set; } = new();

    /// <summary>
    /// 消息队列统计
    /// </summary>
    public MessageQueueStats MessageQueue { get; set; } = new();

    /// <summary>
    /// Saga 统计
    /// </summary>
    public SagaStats Saga { get; set; } = new();
}

/// <summary>
/// 服务发现统计
/// </summary>
public class ServiceDiscoveryStats
{
    /// <summary>
    /// 服务总数
    /// </summary>
    public int TotalServices { get; set; }

    /// <summary>
    /// 实例总数
    /// </summary>
    public int TotalInstances { get; set; }

    /// <summary>
    /// 健康实例数
    /// </summary>
    public int HealthyInstances { get; set; }

    /// <summary>
    /// 不健康实例数
    /// </summary>
    public int UnhealthyInstances { get; set; }

    /// <summary>
    /// 健康率
    /// </summary>
    public double HealthRate => TotalInstances > 0 
        ? (double)HealthyInstances / TotalInstances * 100 
        : 0;
}

/// <summary>
/// 配置中心统计
/// </summary>
public class ConfigCenterStats
{
    /// <summary>
    /// 命名空间总数
    /// </summary>
    public int TotalNamespaces { get; set; }

    /// <summary>
    /// 配置项总数
    /// </summary>
    public int TotalConfigItems { get; set; }

    /// <summary>
    /// 灰度发布中的配置数
    /// </summary>
    public int GrayReleaseCount { get; set; }
}

/// <summary>
/// 网关统计
/// </summary>
public class GatewayStats
{
    /// <summary>
    /// 路由总数
    /// </summary>
    public int TotalRoutes { get; set; }

    /// <summary>
    /// 每秒请求数
    /// </summary>
    public double RequestsPerSecond { get; set; }

    /// <summary>
    /// 平均响应时间（毫秒）
    /// </summary>
    public double AverageResponseTimeMs { get; set; }

    /// <summary>
    /// 错误率
    /// </summary>
    public double ErrorRate { get; set; }
}

/// <summary>
/// 消息队列统计
/// </summary>
public class MessageQueueStats
{
    /// <summary>
    /// Topic 总数
    /// </summary>
    public int TotalTopics { get; set; }

    /// <summary>
    /// 消息总数
    /// </summary>
    public long TotalMessages { get; set; }

    /// <summary>
    /// 待消费消息数
    /// </summary>
    public long PendingMessages { get; set; }

    /// <summary>
    /// 每秒消息吞吐量
    /// </summary>
    public double MessagesPerSecond { get; set; }
}
