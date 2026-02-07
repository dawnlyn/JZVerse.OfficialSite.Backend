namespace JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

/// <summary>
/// 配置版本历史
/// </summary>
public sealed class ConfigVersion
{
    /// <summary>
    /// 版本唯一标识
    /// </summary>
    public required string VersionId { get; init; }

    /// <summary>
    /// 关联配置项 ID
    /// </summary>
    public required string ItemId { get; init; }

    /// <summary>
    /// 版本号
    /// </summary>
    public required long Version { get; init; }

    /// <summary>
    /// 旧值
    /// </summary>
    public string? OldValue { get; init; }

    /// <summary>
    /// 新值
    /// </summary>
    public required string NewValue { get; init; }

    /// <summary>
    /// 变更类型
    /// </summary>
    public ConfigChangeType ChangeType { get; init; }

    /// <summary>
    /// 变更原因
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 创建人
    /// </summary>
    public string? CreatedBy { get; init; }

    /// <summary>
    /// 回滚来源版本（如果是回滚操作）
    /// </summary>
    public long? RollbackFromVersion { get; init; }
}
