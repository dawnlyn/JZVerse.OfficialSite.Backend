namespace JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;

/// <summary>
/// 服务元数据
/// </summary>
public sealed class ServiceMetadata
{
    /// <summary>
    /// 自定义元数据键值对
    /// </summary>
    public Dictionary<string, string> Properties { get; init; } = new();

    /// <summary>
    /// 支持的协议列表
    /// </summary>
    public List<string> SupportedProtocols { get; init; } = [];

    /// <summary>
    /// 服务描述
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 负责人
    /// </summary>
    public string? Owner { get; init; }

    /// <summary>
    /// 环境（dev, test, staging, prod）
    /// </summary>
    public string Environment { get; init; } = "dev";

    /// <summary>
    /// 区域/数据中心
    /// </summary>
    public string? Region { get; init; }

    /// <summary>
    /// 服务健康检查端点
    /// </summary>
    public string? HealthCheckEndpoint { get; init; }

    /// <summary>
    /// 服务管理端点
    /// </summary>
    public string? ManagementEndpoint { get; init; }
}
