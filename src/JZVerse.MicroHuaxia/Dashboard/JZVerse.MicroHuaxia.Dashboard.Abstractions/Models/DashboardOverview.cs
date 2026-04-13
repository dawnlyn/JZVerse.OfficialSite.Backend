namespace JZVerse.MicroHuaxia.Dashboard.Abstractions.Models;

/// <summary>
/// Dashboard 总览数据
/// </summary>
public class DashboardOverview
{
    /// <summary>
    /// 服务发现统计
    /// </summary>
    public ServiceDiscoveryOverview ServiceDiscovery { get; set; } = new();

    /// <summary>
    /// 配置中心统计
    /// </summary>
    public ConfigCenterOverview ConfigCenter { get; set; } = new();

    /// <summary>
    /// 网关统计
    /// </summary>
    public GatewayOverview Gateway { get; set; } = new();

    /// <summary>
    /// 消息队列统计
    /// </summary>
    public MessageQueueOverview MessageQueue { get; set; } = new();

    /// <summary>
    /// Saga 统计
    /// </summary>
    public SagaOverview Saga { get; set; } = new();

    /// <summary>
    /// 流量治理统计
    /// </summary>
    public TrafficOverview Traffic { get; set; } = new();
}

/// <summary>
/// 服务发现统计
/// </summary>
public class ServiceDiscoveryOverview
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
    /// 健康率
    /// </summary>
    public double HealthRate { get; set; }

    /// <summary>
    /// 趋势变化百分比
    /// </summary>
    public double Trend { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    public string Status { get; set; } = "Healthy";
}

/// <summary>
/// 配置中心统计
/// </summary>
public class ConfigCenterOverview
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
    /// 待发布配置数
    /// </summary>
    public int PendingReleases { get; set; }

    /// <summary>
    /// 灰度发布中的配置数
    /// </summary>
    public int GrayReleaseCount { get; set; }
}

/// <summary>
/// 网关统计
/// </summary>
public class GatewayOverview
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
    public double AvgLatencyMs { get; set; }

    /// <summary>
    /// 今日请求总数
    /// </summary>
    public long TodayRequests { get; set; }

    /// <summary>
    /// 趋势变化百分比
    /// </summary>
    public double Trend { get; set; }
}

/// <summary>
/// 消息队列统计
/// </summary>
public class MessageQueueOverview
{
    /// <summary>
    /// Topic 总数
    /// </summary>
    public int TotalTopics { get; set; }

    /// <summary>
    /// 消费者组总数
    /// </summary>
    public int TotalConsumerGroups { get; set; }

    /// <summary>
    /// 待消费消息数
    /// </summary>
    public long PendingMessages { get; set; }

    /// <summary>
    /// 每秒消息吞吐量
    /// </summary>
    public double Throughput { get; set; }

    /// <summary>
    /// 趋势变化百分比
    /// </summary>
    public double Trend { get; set; }

    /// <summary>
    /// 消息总数
    /// </summary>
    public long TotalMessages { get; set; }

    /// <summary>
    /// 每秒消息数
    /// </summary>
    public double MessagesPerSecond { get; set; }
}

/// <summary>
/// Saga 统计
/// </summary>
public class SagaOverview
{
    /// <summary>
    /// 总实例数
    /// </summary>
    public int TotalInstances { get; set; }

    /// <summary>
    /// 执行中数量
    /// </summary>
    public int Executing { get; set; }

    /// <summary>
    /// 失败数量
    /// </summary>
    public int Failed { get; set; }

    /// <summary>
    /// 已完成数量
    /// </summary>
    public int Completed { get; set; }
}

/// <summary>
/// 流量治理统计
/// </summary>
public class TrafficOverview
{
    /// <summary>
    /// 金丝雀任务数
    /// </summary>
    public int CanaryTasks { get; set; }

    /// <summary>
    /// 蓝绿部署任务数
    /// </summary>
    public int BlueGreenTasks { get; set; }

    /// <summary>
    /// 熔断器开启数量
    /// </summary>
    public int CircuitBreakersOpen { get; set; }

    /// <summary>
    /// 限流规则数量
    /// </summary>
    public int RateLimitRules { get; set; }
}

// 保留原有类名以兼容现有代码
public class ServiceDiscoveryStats : ServiceDiscoveryOverview { }
public class ConfigCenterStats : ConfigCenterOverview { }
public class GatewayStats : GatewayOverview { }
public class MessageQueueStats : MessageQueueOverview { }
