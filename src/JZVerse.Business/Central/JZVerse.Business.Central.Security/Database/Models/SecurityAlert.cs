namespace JZVerse.Business.Central.Security.Database.Models;

/// <summary>
/// 安全告警实体
/// </summary>
public class SecurityAlert
{
    /// <summary>
    /// 告警ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 告警类型
    /// </summary>
    public string AlertType { get; set; } = string.Empty;

    /// <summary>
    /// 关联用户ID
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// 告警标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 告警内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 严重级别（Critical/High/Medium/Low）
    /// </summary>
    public string Severity { get; set; } = string.Empty;

    /// <summary>
    /// 关联数据（JSON格式）
    /// </summary>
    public string? RelatedData { get; set; }

    /// <summary>
    /// 触发来源IP
    /// </summary>
    public string? SourceIp { get; set; }

    /// <summary>
    /// 状态（Pending/Processing/Resolved/Ignored）
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 是否已推送
    /// </summary>
    public bool IsPushed { get; set; }

    /// <summary>
    /// 推送渠道
    /// </summary>
    public string? PushChannels { get; set; }

    /// <summary>
    /// 推送时间
    /// </summary>
    public DateTime? PushedAt { get; set; }

    /// <summary>
    /// 处理人
    /// </summary>
    public string? Handler { get; set; }

    /// <summary>
    /// 处理备注
    /// </summary>
    public string? HandleRemark { get; set; }

    /// <summary>
    /// 处理时间
    /// </summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 告警类型常量
/// </summary>
public static class AlertTypes
{
    public const string AbnormalLogin = "AbnormalLogin";
    public const string PasswordError = "PasswordError";
    public const string HighFrequencyOperation = "HighFrequencyOperation";
    public const string SensitiveConfigChange = "SensitiveConfigChange";
    public const string AttackDetected = "AttackDetected";
    public const string RateLimitTriggered = "RateLimitTriggered";
    public const string TokenAbnormal = "TokenAbnormal";
    public const string DataTampering = "DataTampering";
    public const string ServiceError = "ServiceError";
}

/// <summary>
/// 严重级别常量
/// </summary>
public static class SeverityLevels
{
    public const string Critical = "Critical";
    public const string High = "High";
    public const string Medium = "Medium";
    public const string Low = "Low";
}

/// <summary>
/// 告警状态常量
/// </summary>
public static class AlertStatuses
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Resolved = "Resolved";
    public const string Ignored = "Ignored";
}
