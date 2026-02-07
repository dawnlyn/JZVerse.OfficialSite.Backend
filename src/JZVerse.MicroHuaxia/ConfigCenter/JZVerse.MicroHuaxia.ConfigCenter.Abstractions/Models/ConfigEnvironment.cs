namespace JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

/// <summary>
/// 配置环境实体
/// </summary>
public sealed class ConfigEnvironment
{
    /// <summary>
    /// 环境唯一标识
    /// </summary>
    public required string EnvironmentId { get; init; }

    /// <summary>
    /// 环境名称（dev/test/staging/prod）
    /// </summary>
    public required string EnvironmentName { get; init; }

    /// <summary>
    /// 所属应用 ID
    /// </summary>
    public required string ApplicationId { get; init; }

    /// <summary>
    /// 环境描述
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 排序顺序
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; } = true;
}
