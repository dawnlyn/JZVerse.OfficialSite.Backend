namespace JZVerse.Business.Central.Security.Database.Models;

/// <summary>
/// 攻击日志实体
/// </summary>
public class AttackLog
{
    /// <summary>
    /// 日志ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 攻击类型
    /// </summary>
    public string AttackType { get; set; } = string.Empty;

    /// <summary>
    /// 攻击来源IP
    /// </summary>
    public string SourceIp { get; set; } = string.Empty;

    /// <summary>
    /// 目标URL
    /// </summary>
    public string TargetUrl { get; set; } = string.Empty;

    /// <summary>
    /// HTTP方法
    /// </summary>
    public string HttpMethod { get; set; } = string.Empty;

    /// <summary>
    /// 请求数据
    /// </summary>
    public string? RequestData { get; set; }

    /// <summary>
    /// 请求头
    /// </summary>
    public string? RequestHeaders { get; set; }

    /// <summary>
    /// 攻击特征/匹配规则
    /// </summary>
    public string? AttackPattern { get; set; }

    /// <summary>
    /// 风险等级（High/Medium/Low）
    /// </summary>
    public string RiskLevel { get; set; } = string.Empty;

    /// <summary>
    /// 是否已拦截
    /// </summary>
    public bool IsIntercepted { get; set; }

    /// <summary>
    /// 拦截方式
    /// </summary>
    public string? InterceptAction { get; set; }

    /// <summary>
    /// 用户代理
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// 是否已处理
    /// </summary>
    public bool IsHandled { get; set; }

    /// <summary>
    /// 处理人
    /// </summary>
    public string? Handler { get; set; }

    /// <summary>
    /// 处理时间
    /// </summary>
    public DateTime? HandledAt { get; set; }

    /// <summary>
    /// 处理备注
    /// </summary>
    public string? HandleRemark { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 攻击类型常量
/// </summary>
public static class AttackTypes
{
    public const string SqlInjection = "SqlInjection";
    public const string Xss = "Xss";
    public const string Csrf = "Csrf";
    public const string PathTraversal = "PathTraversal";
    public const string CommandInjection = "CommandInjection";
    public const string BruteForce = "BruteForce";
    public const string RateLimitViolation = "RateLimitViolation";
    public const string InvalidToken = "InvalidToken";
    public const string Tampering = "Tampering";
    public const string Other = "Other";
}

/// <summary>
/// 风险等级常量
/// </summary>
public static class RiskLevels
{
    public const string High = "High";
    public const string Medium = "Medium";
    public const string Low = "Low";
}
