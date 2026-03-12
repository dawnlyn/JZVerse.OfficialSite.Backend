namespace JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;

/// <summary>
/// 服务实例信息
/// </summary>
public sealed class ServiceInstance
{
    /// <summary>
    /// 实例唯一标识（自动生成或手动指定）
    /// </summary>
    public string InstanceId { get; init; } = string.Empty;

    /// <summary>
    /// 服务名称
    /// </summary>
    public string ServiceName { get; init; } = string.Empty;

    /// <summary>
    /// 服务版本
    /// </summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    /// 主机地址（IP 或域名）
    /// </summary>
    public string Host { get; init; } = string.Empty;

    /// <summary>
    /// 端口号（可选，域名形式可不指定）
    /// </summary>
    public int? Port { get; init; }

    /// <summary>
    /// 协议（http, https, grpc, tcp）
    /// </summary>
    public string Scheme { get; init; } = "http";

    /// <summary>
    /// 服务基础路径
    /// </summary>
    public string? BasePath { get; init; }

    /// <summary>
    /// 完整的服务地址（自动计算）
    /// </summary>
    public string Address
    {
        get
        {
            var hostPort = Port.HasValue && Port.Value > 0 ? $"{Host}:{Port}" : Host;
            return string.IsNullOrEmpty(BasePath) ? $"{Scheme}://{hostPort}" : $"{Scheme}://{hostPort}{BasePath}";
        }
    }

    /// <summary>
    /// 服务标签（用于分组、灰度等）
    /// </summary>
    public HashSet<string> Tags { get; init; } = [];

    /// <summary>
    /// 服务元数据
    /// </summary>
    public ServiceMetadata Metadata { get; init; } = new();

    /// <summary>
    /// 健康状态
    /// </summary>
    public HealthStatus Health { get; set; } = HealthStatus.Unknown;

    /// <summary>
    /// 注册时间
    /// </summary>
    public DateTimeOffset RegisteredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 最后心跳时间
    /// </summary>
    public DateTimeOffset LastHeartbeatAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 最后健康检查时间
    /// </summary>
    public DateTimeOffset LastHealthCheckAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 连续失败次数
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// 权重（负载均衡使用）
    /// </summary>
    public int Weight { get; init; } = 100;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 注销时间（null 表示仍在线）
    /// </summary>
    public DateTimeOffset? DeregisteredAt { get; set; }

    /// <summary>
    /// 是否已注销
    /// </summary>
    public bool IsDeregistered => DeregisteredAt.HasValue;
}
