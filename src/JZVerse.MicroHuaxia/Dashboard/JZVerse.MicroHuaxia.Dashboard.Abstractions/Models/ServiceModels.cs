namespace JZVerse.MicroHuaxia.Dashboard.Abstractions.Models;

/// <summary>
/// 服务信息
/// </summary>
public class ServiceInfo
{
    /// <summary>
    /// 服务名称
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// 实例数量
    /// </summary>
    public int InstanceCount { get; set; }

    /// <summary>
    /// 健康实例数量
    /// </summary>
    public int HealthyCount { get; set; }

    /// <summary>
    /// 不健康实例数量
    /// </summary>
    public int UnhealthyCount { get; set; }

    /// <summary>
    /// 服务状态
    /// </summary>
    public ServiceStatus Status { get; set; }
}

/// <summary>
/// 服务实例信息
/// </summary>
public class ServiceInstance
{
    /// <summary>
    /// 实例 ID
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// 服务名称
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// 主机地址
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// 端口
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// 健康状态
    /// </summary>
    public HealthStatus HealthStatus { get; set; }

    /// <summary>
    /// 权重
    /// </summary>
    public int Weight { get; set; } = 100;

    /// <summary>
    /// 元数据
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// 注册时间
    /// </summary>
    public DateTime RegisterTime { get; set; }

    /// <summary>
    /// 最后心跳时间
    /// </summary>
    public DateTime LastHeartbeat { get; set; }
}

/// <summary>
/// 服务状态
/// </summary>
public enum ServiceStatus
{
    /// <summary>
    /// 健康
    /// </summary>
    Healthy,

    /// <summary>
    /// 部分健康
    /// </summary>
    PartialHealthy,

    /// <summary>
    /// 不健康
    /// </summary>
    Unhealthy,

    /// <summary>
    /// 未知
    /// </summary>
    Unknown
}

/// <summary>
/// 健康状态
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// 未知（初始状态）
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 健康
    /// </summary>
    Healthy = 1,

    /// <summary>
    /// 不健康
    /// </summary>
    Unhealthy = 2,

    /// <summary>
    /// 降级（部分功能不可用但服务仍可用）
    /// </summary>
    Degraded = 3,

    /// <summary>
    /// 启动中
    /// </summary>
    Starting = 4,

    /// <summary>
    /// 停止中
    /// </summary>
    Stopping = 5,
}
