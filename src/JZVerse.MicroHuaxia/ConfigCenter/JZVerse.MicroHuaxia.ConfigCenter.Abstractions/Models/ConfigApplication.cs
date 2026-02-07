namespace JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

/// <summary>
/// 配置应用实体
/// </summary>
public sealed class ConfigApplication
{
    /// <summary>
    /// 应用唯一标识
    /// </summary>
    public required string ApplicationId { get; init; }

    /// <summary>
    /// 应用名称
    /// </summary>
    public required string ApplicationName { get; init; }

    /// <summary>
    /// 应用描述
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 负责人
    /// </summary>
    public string? Owner { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 应用元数据
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; } = true;
}
