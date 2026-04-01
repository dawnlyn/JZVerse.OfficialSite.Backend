namespace JZVerse.Business.Central.Security;

/// <summary>
/// 安全配置选项
/// </summary>
public class SecurityOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Security";

    /// <summary>
    /// 是否启用攻击检测
    /// </summary>
    public bool EnableAttackDetection { get; set; } = true;

    /// <summary>
    /// 是否启用SQL注入检测
    /// </summary>
    public bool EnableSqlInjectionDetection { get; set; } = true;

    /// <summary>
    /// 是否启用XSS检测
    /// </summary>
    public bool EnableXssDetection { get; set; } = true;

    /// <summary>
    /// 是否启用CSRF检测
    /// </summary>
    public bool EnableCsrfDetection { get; set; } = true;

    /// <summary>
    /// 是否启用路径遍历检测
    /// </summary>
    public bool EnablePathTraversalDetection { get; set; } = true;

    /// <summary>
    /// 是否启用命令注入检测
    /// </summary>
    public bool EnableCommandInjectionDetection { get; set; } = true;

    /// <summary>
    /// CSRF Token过期时间（小时）
    /// </summary>
    public int CsrfTokenExpirationHours { get; set; } = 2;

    /// <summary>
    /// 允许的域名列表（用于CSRF检查）
    /// </summary>
    public List<string> AllowedDomains { get; set; } = new();

    /// <summary>
    /// IP封禁配置
    /// </summary>
    public IpBanOptions IpBan { get; set; } = new();

    /// <summary>
    /// 告警配置
    /// </summary>
    public AlertOptions Alert { get; set; } = new();
}

/// <summary>
/// IP封禁配置
/// </summary>
public class IpBanOptions
{
    /// <summary>
    /// 是否启用自动封禁
    /// </summary>
    public bool EnableAutoBan { get; set; } = true;

    /// <summary>
    /// 触发自动封禁的攻击次数
    /// </summary>
    public int AutoBanThreshold { get; set; } = 5;

    /// <summary>
    /// 自动封禁时长（分钟，0表示永久）
    /// </summary>
    public int AutoBanDurationMinutes { get; set; } = 60;

    /// <summary>
    /// 封禁检查时间窗口（分钟）
    /// </summary>
    public int BanCheckWindowMinutes { get; set; } = 10;
}

/// <summary>
/// 告警配置
/// </summary>
public class AlertOptions
{
    /// <summary>
    /// 是否启用自动告警
    /// </summary>
    public bool EnableAutoAlert { get; set; } = true;

    /// <summary>
    /// 是否启用自动推送
    /// </summary>
    public bool EnableAutoPush { get; set; } = true;

    /// <summary>
    /// 最小告警级别（低于此级别不触发告警）
    /// </summary>
    public string MinAlertSeverity { get; set; } = "Low";

    /// <summary>
    /// 告警冷却时间（分钟）
    /// </summary>
    public int AlertCooldownMinutes { get; set; } = 5;
}
