namespace JZVerse.MicroHuaxia.Security.Audit;

/// <summary>
/// 审计日志记录器接口
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// 记录访问请求审计事件
    /// </summary>
    /// <param name="request">访问请求</param>
    /// <param name="decision">访问决策</param>
    /// <param name="properties">扩展属性</param>
    Task LogAccessAsync(
        AccessRequest request,
        AccessDecision decision,
        IReadOnlyDictionary<string, object>? properties = null);

    /// <summary>
    /// 记录认证审计事件
    /// </summary>
    /// <param name="identity">身份信息</param>
    /// <param name="method">认证方式</param>
    /// <param name="success">是否成功</param>
    /// <param name="reason">失败原因</param>
    /// <param name="properties">扩展属性</param>
    Task LogAuthenticationAsync(
        SecurityIdentity? identity,
        AuthenticationMethod method,
        bool success,
        string? reason = null,
        IReadOnlyDictionary<string, object>? properties = null);

    /// <summary>
    /// 记录操作审计事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="level">审计级别</param>
    /// <param name="identity">身份信息</param>
    /// <param name="resource">资源</param>
    /// <param name="action">操作</param>
    /// <param name="success">是否成功</param>
    /// <param name="properties">扩展属性</param>
    Task LogOperationAsync(
        string eventType,
        AuditLevel level,
        SecurityIdentity? identity,
        string? resource = null,
        string? action = null,
        bool success = true,
        IReadOnlyDictionary<string, object>? properties = null);

    /// <summary>
    /// 记录安全事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="severity">严重程度</param>
    /// <param name="message">事件消息</param>
    /// <param name="identity">身份信息</param>
    /// <param name="properties">扩展属性</param>
    Task LogSecurityEventAsync(
        string eventType,
        SecurityEventSeverity severity,
        string message,
        SecurityIdentity? identity = null,
        IReadOnlyDictionary<string, object>? properties = null);

    /// <summary>
    /// 刷新审计缓冲区
    /// </summary>
    Task FlushAsync();
}

/// <summary>
/// 安全事件严重程度
/// </summary>
public enum SecurityEventSeverity
{
    /// <summary>
    /// 信息
    /// </summary>
    Info = 0,

    /// <summary>
    /// 低危
    /// </summary>
    Low = 1,

    /// <summary>
    /// 中危
    /// </summary>
    Medium = 2,

    /// <summary>
    /// 高危
    /// </summary>
    High = 3,

    /// <summary>
    /// 严重
    /// </summary>
    Critical = 4
}
