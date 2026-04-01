namespace JZVerse.Business.Central.Security.Database.Models;

/// <summary>
/// 操作审计日志实体
/// </summary>
public class AuditLog
{
    /// <summary>
    /// 日志ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 操作用户ID
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// 操作用户名
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// 操作类型
    /// </summary>
    public string OperationType { get; set; } = string.Empty;

    /// <summary>
    /// 操作模块
    /// </summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>
    /// 操作内容描述
    /// </summary>
    public string OperationContent { get; set; } = string.Empty;

    /// <summary>
    /// 请求数据（JSON格式）
    /// </summary>
    public string? RequestData { get; set; }

    /// <summary>
    /// 响应数据（JSON格式）
    /// </summary>
    public string? ResponseData { get; set; }

    /// <summary>
    /// 操作结果（Success/Failure）
    /// </summary>
    public string Result { get; set; } = string.Empty;

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// IP地址
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// 用户代理
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// 请求路径
    /// </summary>
    public string RequestPath { get; set; } = string.Empty;

    /// <summary>
    /// HTTP方法
    /// </summary>
    public string HttpMethod { get; set; } = string.Empty;

    /// <summary>
    /// 执行时长（毫秒）
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// 是否为敏感操作
    /// </summary>
    public bool IsSensitive { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 操作类型常量
/// </summary>
public static class AuditOperationTypes
{
    public const string Login = "Login";
    public const string Logout = "Logout";
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Delete = "Delete";
    public const string Query = "Query";
    public const string Export = "Export";
    public const string ConfigChange = "ConfigChange";
}
