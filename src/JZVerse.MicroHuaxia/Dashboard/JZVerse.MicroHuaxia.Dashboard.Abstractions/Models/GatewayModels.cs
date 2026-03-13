namespace JZVerse.MicroHuaxia.Dashboard.Abstractions.Models;

/// <summary>
/// 路由信息
/// </summary>
public class RouteInfo
{
    /// <summary>
    /// 路由 ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 路由名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 匹配路径
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// HTTP 方法
    /// </summary>
    public List<string> Methods { get; set; } = new();

    /// <summary>
    /// 目标服务名称
    /// </summary>
    public string TargetService { get; set; } = string.Empty;

    /// <summary>
    /// 目标路径
    /// </summary>
    public string TargetPath { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 优先级
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// 负载均衡策略
    /// </summary>
    public string LoadBalancer { get; set; } = "RoundRobin";

    /// <summary>
    /// 超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// 审计日志
/// </summary>
public class AuditLog
{
    /// <summary>
    /// 日志 ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 请求时间
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// 请求路径
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// HTTP 方法
    /// </summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// 响应状态码
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// 响应时间（毫秒）
    /// </summary>
    public long ResponseTimeMs { get; set; }

    /// <summary>
    /// 客户端 IP
    /// </summary>
    public string ClientIp { get; set; } = string.Empty;

    /// <summary>
    /// 目标服务
    /// </summary>
    public string TargetService { get; set; } = string.Empty;

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 审计日志查询参数
/// </summary>
public class AuditLogQuery
{
    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 路径过滤
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// 状态码过滤
    /// </summary>
    public int? StatusCode { get; set; }

    /// <summary>
    /// 页码
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// 每页数量
    /// </summary>
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// 仅显示业务服务日志（TargetService 有值的记录）
    /// </summary>
    public bool BusinessOnly { get; set; } = true;
}
