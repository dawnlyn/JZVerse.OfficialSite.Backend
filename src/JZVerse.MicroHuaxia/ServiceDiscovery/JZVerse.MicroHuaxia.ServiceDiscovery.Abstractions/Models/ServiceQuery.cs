namespace JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;

/// <summary>
/// 服务查询条件
/// </summary>
public sealed class ServiceQuery
{
    /// <summary>
    /// 服务名称（支持通配符 *）
    /// </summary>
    public string? ServiceName { get; init; }

    /// <summary>
    /// 服务版本
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// 标签（AND 关系）
    /// </summary>
    public List<string>? Tags { get; init; }

    /// <summary>
    /// 健康状态过滤
    /// </summary>
    public HealthStatus? HealthStatus { get; init; }

    /// <summary>
    /// 环境
    /// </summary>
    public string? Environment { get; init; }

    /// <summary>
    /// 区域
    /// </summary>
    public string? Region { get; init; }

    /// <summary>
    /// 只返回启用的实例
    /// </summary>
    public bool OnlyEnabled { get; init; } = true;

    /// <summary>
    /// 只返回健康的实例
    /// </summary>
    public bool OnlyHealthy { get; init; } = true;

    /// <summary>
    /// 是否包含已注销的实例（默认不包含）
    /// </summary>
    public bool IncludeDeregistered { get; init; } = false;
}
