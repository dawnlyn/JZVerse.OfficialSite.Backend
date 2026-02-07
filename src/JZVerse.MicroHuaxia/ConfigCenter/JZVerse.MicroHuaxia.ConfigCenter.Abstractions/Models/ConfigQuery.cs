namespace JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

/// <summary>
/// 配置查询条件
/// </summary>
public sealed class ConfigQuery
{
    /// <summary>
    /// 应用 ID
    /// </summary>
    public required string ApplicationId { get; init; }

    /// <summary>
    /// 环境 ID
    /// </summary>
    public required string EnvironmentId { get; init; }

    /// <summary>
    /// 命名空间 ID（可选，不指定则查询所有命名空间）
    /// </summary>
    public string? NamespaceId { get; init; }

    /// <summary>
    /// 指定键列表（可选，不指定则返回所有配置）
    /// </summary>
    public List<string>? Keys { get; init; }

    /// <summary>
    /// 是否包含敏感配置
    /// </summary>
    public bool IncludeSecrets { get; init; }

    /// <summary>
    /// 返回格式（可选）
    /// </summary>
    public ConfigFormat? Format { get; init; }

    /// <summary>
    /// 客户端信息（用于灰度判断）
    /// </summary>
    public ClientInfo? ClientInfo { get; init; }
}
