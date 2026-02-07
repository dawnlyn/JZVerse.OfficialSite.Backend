namespace JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

/// <summary>
/// 灰度发布配置
/// </summary>
public sealed record GrayRelease
{
    /// <summary>
    /// 发布唯一标识
    /// </summary>
    public required string ReleaseId { get; init; }

    /// <summary>
    /// 发布名称
    /// </summary>
    public required string ReleaseName { get; init; }

    /// <summary>
    /// 命名空间 ID
    /// </summary>
    public required string NamespaceId { get; init; }

    /// <summary>
    /// 环境 ID
    /// </summary>
    public required string EnvironmentId { get; init; }

    /// <summary>
    /// 灰度策略
    /// </summary>
    public GrayReleaseStrategy Strategy { get; init; } = GrayReleaseStrategy.Manual;

    /// <summary>
    /// 灰度规则列表
    /// </summary>
    public List<GrayReleaseRule> TargetRules { get; init; } = [];

    /// <summary>
    /// 发布状态
    /// </summary>
    public GrayReleaseStatus Status { get; set; } = GrayReleaseStatus.Draft;

    /// <summary>
    /// 发布百分比（0-100）
    /// </summary>
    public int RolloutPercentage { get; set; }

    /// <summary>
    /// 配置快照（灰度版本的配置）
    /// </summary>
    public Dictionary<string, string> ConfigSnapshot { get; init; } = new();

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// 完成时间
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// 创建人
    /// </summary>
    public string? CreatedBy { get; init; }
}
