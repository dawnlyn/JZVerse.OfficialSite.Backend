namespace JZVerse.MicroHuaxia.ServiceDiscovery.Server.Configuration;

/// <summary>
/// 服务发现服务端配置选项
/// </summary>
public class ServiceDiscoveryServerOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "ServiceDiscovery:Server";

    /// <summary>
    /// 服务名称
    /// </summary>
    public string ServiceName { get; set; } = "service-discovery-server";

    /// <summary>
    /// 健康检查间隔（秒）
    /// </summary>
    public int HealthCheckIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// 健康检查超时（秒）
    /// </summary>
    public int HealthCheckTimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// 心跳超时（秒）
    /// </summary>
    public int HeartbeatTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 失败阈值
    /// </summary>
    public int FailureThreshold { get; set; } = 3;

    /// <summary>
    /// 负载均衡策略
    /// </summary>
    public string LoadBalancer { get; set; } = "RoundRobin";

    /// <summary>
    /// 是否启用缓存
    /// </summary>
    public bool EnableCache { get; set; } = true;

    /// <summary>
    /// 缓存 TTL（秒）
    /// </summary>
    public int CacheTtlSeconds { get; set; } = 60;
}
