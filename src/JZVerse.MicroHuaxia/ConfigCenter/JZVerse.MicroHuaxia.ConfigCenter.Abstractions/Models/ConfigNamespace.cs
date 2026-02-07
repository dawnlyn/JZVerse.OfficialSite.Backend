namespace JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

/// <summary>
/// 配置命名空间实体
/// </summary>
public sealed class ConfigNamespace
{
    /// <summary>
    /// 命名空间唯一标识
    /// </summary>
    public required string NamespaceId { get; init; }

    /// <summary>
    /// 命名空间名称（如：database、redis、logging）
    /// </summary>
    public required string NamespaceName { get; init; }

    /// <summary>
    /// 所属应用 ID
    /// </summary>
    public required string ApplicationId { get; init; }

    /// <summary>
    /// 配置格式
    /// </summary>
    public ConfigFormat Format { get; init; } = ConfigFormat.JSON;

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 是否为公共配置（可被其他应用引用）
    /// </summary>
    public bool IsPublic { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
