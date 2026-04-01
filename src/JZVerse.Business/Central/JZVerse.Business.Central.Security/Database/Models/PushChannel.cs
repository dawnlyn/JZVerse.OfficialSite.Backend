namespace JZVerse.Business.Central.Security.Database.Models;

/// <summary>
/// 推送渠道配置实体
/// </summary>
public class PushChannel
{
    /// <summary>
    /// 渠道ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 渠道类型
    /// </summary>
    public string ChannelType { get; set; } = string.Empty;

    /// <summary>
    /// 渠道名称
    /// </summary>
    public string ChannelName { get; set; } = string.Empty;

    /// <summary>
    /// 配置JSON
    /// </summary>
    public string ConfigJson { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 支持的告警级别（逗号分隔）
    /// </summary>
    public string? SupportedSeverities { get; set; }

    /// <summary>
    /// 支持的告警类型（逗号分隔，空表示全部）
    /// </summary>
    public string? SupportedAlertTypes { get; set; }

    /// <summary>
    /// 每日推送限制（0表示无限制）
    /// </summary>
    public int DailyPushLimit { get; set; }

    /// <summary>
    /// 今日推送次数
    /// </summary>
    public int TodayPushCount { get; set; }

    /// <summary>
    /// 最后推送时间
    /// </summary>
    public DateTime? LastPushAt { get; set; }

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 渠道类型常量
/// </summary>
public static class ChannelTypes
{
    public const string WeChatWork = "WeChatWork";
    public const string DingTalk = "DingTalk";
    public const string FeiShu = "FeiShu";
    public const string Email = "Email";
    public const string Sms = "Sms";
    public const string Webhook = "Webhook";
}

/// <summary>
/// 企业微信配置
/// </summary>
public class WeChatWorkConfig
{
    public string CorpId { get; set; } = string.Empty;
    public string CorpSecret { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public string? WebhookKey { get; set; }
}

/// <summary>
/// 钉钉配置
/// </summary>
public class DingTalkConfig
{
    public string AppKey { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
    public string? WebhookToken { get; set; }
    public string? Secret { get; set; }
}

/// <summary>
/// 飞书配置
/// </summary>
public class FeiShuConfig
{
    public string AppId { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
    public string? WebhookToken { get; set; }
}
