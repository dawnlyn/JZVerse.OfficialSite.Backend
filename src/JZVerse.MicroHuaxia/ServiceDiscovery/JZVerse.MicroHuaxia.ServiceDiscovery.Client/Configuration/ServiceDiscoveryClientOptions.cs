namespace JZVerse.MicroHuaxia.ServiceDiscovery.Client.Configuration;

/// <summary>
/// 服务发现客户端配置选项
/// </summary>
public class ServiceDiscoveryClientOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "ServiceDiscovery:Client";

    /// <summary>
    /// 注册中心服务器地址列表
    /// </summary>
    public List<string> ServerUrls { get; set; } = ["http://localhost:5100"];

    /// <summary>
    /// 通信协议（Http 或 Grpc）
    /// </summary>
    public string Protocol { get; set; } = "Http";

    /// <summary>
    /// 服务配置
    /// </summary>
    public ServiceOptions Service { get; set; } = new();

    /// <summary>
    /// 心跳配置
    /// </summary>
    public HeartbeatOptions Heartbeat { get; set; } = new();

    /// <summary>
    /// 健康检查配置
    /// </summary>
    public ClientHealthCheckOptions HealthCheck { get; set; } = new();

    /// <summary>
    /// 发现配置
    /// </summary>
    public DiscoveryOptions Discovery { get; set; } = new();
}

/// <summary>
/// 服务配置
/// </summary>
public class ServiceOptions
{
    /// <summary>
    /// 服务名称
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// 服务版本
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// 主机地址
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// 端口号
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// 协议
    /// </summary>
    public string Scheme { get; set; } = "http";

    /// <summary>
    /// 基础路径
    /// </summary>
    public string? BasePath { get; set; }

    /// <summary>
    /// 标签
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// 元数据
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// 权重
    /// </summary>
    public int Weight { get; set; } = 100;

    /// <summary>
    /// 是否自动注册
    /// </summary>
    public bool AutoRegister { get; set; } = true;

    /// <summary>
    /// 是否自动注销
    /// </summary>
    public bool AutoDeregister { get; set; } = true;
}

/// <summary>
/// 心跳配置
/// </summary>
public class HeartbeatOptions
{
    /// <summary>
    /// 是否启用心跳
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 心跳间隔（秒）
    /// </summary>
    public int IntervalSeconds { get; set; } = 30;
}

/// <summary>
/// 客户端健康检查配置
/// </summary>
public class ClientHealthCheckOptions
{
    /// <summary>
    /// 是否启用健康检查端点
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 健康检查端点
    /// </summary>
    public string Endpoint { get; set; } = "/health";
}

/// <summary>
/// 发现配置
/// </summary>
public class DiscoveryOptions
{
    /// <summary>
    /// 是否启用缓存
    /// </summary>
    public bool EnableCache { get; set; } = true;

    /// <summary>
    /// 缓存 TTL（秒）
    /// </summary>
    public int CacheTtlSeconds { get; set; } = 60;

    /// <summary>
    /// 刷新间隔（秒）
    /// </summary>
    public int RefreshIntervalSeconds { get; set; } = 120;

    /// <summary>
    /// 是否启用故障转移
    /// </summary>
    public bool EnableFailover { get; set; } = true;

    /// <summary>
    /// 负载均衡策略
    /// </summary>
    public string LoadBalancer { get; set; } = "RoundRobin";
}
