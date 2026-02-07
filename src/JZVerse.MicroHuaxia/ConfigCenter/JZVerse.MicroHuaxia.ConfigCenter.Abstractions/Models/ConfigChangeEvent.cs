namespace JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

/// <summary>
/// 配置变更事件
/// </summary>
public sealed class ConfigChangeEvent
{
    /// <summary>
    /// 事件 ID
    /// </summary>
    public required string EventId { get; init; }

    /// <summary>
    /// 事件类型
    /// </summary>
    public ConfigEventType EventType { get; init; }

    /// <summary>
    /// 应用 ID
    /// </summary>
    public required string ApplicationId { get; init; }

    /// <summary>
    /// 环境 ID
    /// </summary>
    public required string EnvironmentId { get; init; }

    /// <summary>
    /// 命名空间 ID
    /// </summary>
    public required string NamespaceId { get; init; }

    /// <summary>
    /// 变更的配置项列表
    /// </summary>
    public List<ConfigItem> ChangedItems { get; init; } = [];

    /// <summary>
    /// 事件时间戳
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 触发人
    /// </summary>
    public string? TriggeredBy { get; init; }

    /// <summary>
    /// 附加数据
    /// </summary>
    public Dictionary<string, object>? AdditionalData { get; init; }
}
