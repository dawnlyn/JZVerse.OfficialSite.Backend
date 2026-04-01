namespace JZVerse.Business.Central.Security.Database.Models;

/// <summary>
/// 限流记录实体
/// </summary>
public class RateLimitRecord
{
    /// <summary>
    /// 记录ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 限流键类型（Ip/User/Endpoint）
    /// </summary>
    public string KeyType { get; set; } = string.Empty;

    /// <summary>
    /// 限流键值
    /// </summary>
    public string KeyValue { get; set; } = string.Empty;

    /// <summary>
    /// 请求计数
    /// </summary>
    public int RequestCount { get; set; }

    /// <summary>
    /// 窗口开始时间
    /// </summary>
    public DateTime WindowStart { get; set; }

    /// <summary>
    /// 窗口结束时间
    /// </summary>
    public DateTime WindowEnd { get; set; }

    /// <summary>
    /// 是否已触发限流
    /// </summary>
    public bool IsLimited { get; set; }

    /// <summary>
    /// 限流触发次数
    /// </summary>
    public int LimitTriggeredCount { get; set; }

    /// <summary>
    /// 最后请求时间
    /// </summary>
    public DateTime LastRequestAt { get; set; }

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
/// 限流键类型常量
/// </summary>
public static class RateLimitKeyTypes
{
    public const string Ip = "Ip";
    public const string User = "User";
    public const string Endpoint = "Endpoint";
    public const string IpAndEndpoint = "IpAndEndpoint";
    public const string UserAndEndpoint = "UserAndEndpoint";
}
