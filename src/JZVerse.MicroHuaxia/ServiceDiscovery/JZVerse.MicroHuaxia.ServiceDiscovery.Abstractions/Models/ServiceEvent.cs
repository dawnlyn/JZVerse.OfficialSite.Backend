namespace JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;

/// <summary>
/// 服务事件类型
/// </summary>
public enum ServiceEventType
{
    /// <summary>
    /// 服务已注册
    /// </summary>
    Registered,

    /// <summary>
    /// 服务已注销
    /// </summary>
    Deregistered,

    /// <summary>
    /// 健康状态变更
    /// </summary>
    HealthChanged,

    /// <summary>
    /// 元数据更新
    /// </summary>
    MetadataUpdated,
}

/// <summary>
/// 服务事件
/// </summary>
public sealed class ServiceEvent
{
    /// <summary>
    /// 事件类型
    /// </summary>
    public ServiceEventType EventType { get; init; }

    /// <summary>
    /// 相关服务实例
    /// </summary>
    public required ServiceInstance Instance { get; init; }

    /// <summary>
    /// 事件时间戳
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 附加数据
    /// </summary>
    public Dictionary<string, object>? AdditionalData { get; init; }
}
