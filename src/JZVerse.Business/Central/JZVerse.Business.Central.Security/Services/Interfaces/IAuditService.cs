using JZVerse.Business.Central.Security.Database.Models;

namespace JZVerse.Business.Central.Security.Services.Interfaces;

/// <summary>
/// 审计服务接口
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// 记录操作日志
    /// </summary>
    /// <param name="entry">审计日志条目</param>
    /// <returns>是否成功</returns>
    Task<bool> LogAsync(AuditLogEntry entry);

    /// <summary>
    /// 记录操作日志（简化版）
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="username">用户名</param>
    /// <param name="operationType">操作类型</param>
    /// <param name="module">模块</param>
    /// <param name="content">操作内容</param>
    /// <param name="isSensitive">是否敏感操作</param>
    /// <returns>是否成功</returns>
    Task<bool> LogAsync(Guid? userId, string? username, string operationType, string module, string content, bool isSensitive = false);

    /// <summary>
    /// 查询审计日志
    /// </summary>
    /// <param name="query">查询条件</param>
    /// <returns>审计日志列表和总数</returns>
    Task<(List<AuditLog> Logs, int Total)> QueryAsync(AuditLogQuery query);

    /// <summary>
    /// 获取单条审计日志详情
    /// </summary>
    /// <param name="id">日志ID</param>
    /// <returns>审计日志</returns>
    Task<AuditLog?> GetByIdAsync(Guid id);

    /// <summary>
    /// 获取用户的最近操作日志
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="count">数量</param>
    /// <returns>审计日志列表</returns>
    Task<List<AuditLog>> GetRecentByUserAsync(Guid userId, int count = 10);

    /// <summary>
    /// 清理过期审计日志
    /// </summary>
    /// <param name="beforeDate">截止日期</param>
    /// <returns>清理数量</returns>
    Task<int> CleanOldLogsAsync(DateTime beforeDate);

    /// <summary>
    /// 统计操作类型分布
    /// </summary>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <returns>统计结果</returns>
    Task<Dictionary<string, int>> StatisticsByOperationTypeAsync(DateTime startTime, DateTime endTime);

    /// <summary>
    /// 统计模块操作分布
    /// </summary>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <returns>统计结果</returns>
    Task<Dictionary<string, int>> StatisticsByModuleAsync(DateTime startTime, DateTime endTime);

    /// <summary>
    /// 健康检查
    /// </summary>
    Task<bool> HealthCheckAsync();
}

/// <summary>
/// 审计日志条目
/// </summary>
public class AuditLogEntry
{
    /// <summary>
    /// 用户ID
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// 操作类型
    /// </summary>
    public string OperationType { get; set; } = string.Empty;

    /// <summary>
    /// 模块
    /// </summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>
    /// 操作内容
    /// </summary>
    public string OperationContent { get; set; } = string.Empty;

    /// <summary>
    /// 请求数据
    /// </summary>
    public object? RequestData { get; set; }

    /// <summary>
    /// 响应数据
    /// </summary>
    public object? ResponseData { get; set; }

    /// <summary>
    /// 操作结果
    /// </summary>
    public string Result { get; set; } = "Success";

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
}

/// <summary>
/// 审计日志查询条件
/// </summary>
public class AuditLogQuery
{
    /// <summary>
    /// 用户ID
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// 操作类型
    /// </summary>
    public string? OperationType { get; set; }

    /// <summary>
    /// 模块
    /// </summary>
    public string? Module { get; set; }

    /// <summary>
    /// 操作结果
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// 是否敏感操作
    /// </summary>
    public bool? IsSensitive { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 页码
    /// </summary>
    public int PageIndex { get; set; } = 1;

    /// <summary>
    /// 每页数量
    /// </summary>
    public int PageSize { get; set; } = 20;
}
