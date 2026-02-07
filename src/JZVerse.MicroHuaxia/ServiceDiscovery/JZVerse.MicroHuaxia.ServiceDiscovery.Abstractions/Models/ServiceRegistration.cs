namespace JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;

/// <summary>
/// 服务注册请求
/// </summary>
public sealed class ServiceRegistration
{
    /// <summary>
    /// 实例 ID（可选，不提供则自动生成）
    /// </summary>
    public string? InstanceId { get; init; }

    /// <summary>
    /// 服务名称（必填）
    /// </summary>
    public required string ServiceName { get; init; }

    /// <summary>
    /// 服务版本
    /// </summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    /// 主机地址（必填，IP 或域名）
    /// </summary>
    public required string Host { get; init; }

    /// <summary>
    /// 端口号（可选，域名形式可不指定）
    /// </summary>
    public int? Port { get; init; }

    /// <summary>
    /// 协议
    /// </summary>
    public string Scheme { get; init; } = "http";

    /// <summary>
    /// 基础路径
    /// </summary>
    public string? BasePath { get; init; }

    /// <summary>
    /// 标签
    /// </summary>
    public List<string> Tags { get; init; } = [];

    /// <summary>
    /// 元数据
    /// </summary>
    public ServiceMetadata Metadata { get; init; } = new();

    /// <summary>
    /// 权重
    /// </summary>
    public int Weight { get; init; } = 100;

    /// <summary>
    /// 心跳间隔（秒）
    /// </summary>
    public int HeartbeatIntervalSeconds { get; init; } = 30;

    /// <summary>
    /// 健康检查配置
    /// </summary>
    public HealthCheckConfiguration? HealthCheck { get; init; }
}

/// <summary>
/// 健康检查配置
/// </summary>
public sealed class HealthCheckConfiguration
{
    /// <summary>
    /// 启用主动健康检查
    /// </summary>
    public bool EnableActiveCheck { get; init; } = true;

    /// <summary>
    /// 主动检查间隔（秒）
    /// </summary>
    public int ActiveCheckIntervalSeconds { get; init; } = 10;

    /// <summary>
    /// 健康检查端点
    /// </summary>
    public string Endpoint { get; init; } = "/health";

    /// <summary>
    /// 超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; init; } = 5;

    /// <summary>
    /// 失败阈值（连续失败多少次标记为不健康）
    /// </summary>
    public int FailureThreshold { get; init; } = 3;

    /// <summary>
    /// 成功阈值（连续成功多少次恢复健康）
    /// </summary>
    public int SuccessThreshold { get; init; } = 2;
}
