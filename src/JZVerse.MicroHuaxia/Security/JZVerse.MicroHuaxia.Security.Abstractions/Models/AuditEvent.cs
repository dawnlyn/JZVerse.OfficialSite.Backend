namespace JZVerse.MicroHuaxia.Security;

/// <summary>
/// 审计事件
/// </summary>
public sealed record AuditEvent
{
    /// <summary>
    /// 事件 ID
    /// </summary>
    public string EventId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 事件时间戳
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 事件类型
    /// </summary>
    public string EventType { get; init; } = null!;

    /// <summary>
    /// 审计级别
    /// </summary>
    public AuditLevel Level { get; init; }

    /// <summary>
    /// 事件主体
    /// </summary>
    public SecurityIdentity? Subject { get; init; }

    /// <summary>
    /// 目标资源
    /// </summary>
    public string? Resource { get; init; }

    /// <summary>
    /// 操作类型
    /// </summary>
    public string? Action { get; init; }

    /// <summary>
    /// 操作结果
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 结果详情
    /// </summary>
    public string? ResultDetail { get; init; }

    /// <summary>
    /// 请求 ID
    /// </summary>
    public string? RequestId { get; init; }

    /// <summary>
    /// 链路追踪 ID
    /// </summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// 来源 IP 地址
    /// </summary>
    public string? SourceIp { get; init; }

    /// <summary>
    /// 用户代理
    /// </summary>
    public string? UserAgent { get; init; }

    /// <summary>
    /// 扩展属性
    /// </summary>
    public IReadOnlyDictionary<string, object> Properties { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// 完整性哈希（防篡改）
    /// </summary>
    public string? IntegrityHash { get; init; }

    /// <summary>
    /// 数字签名
    /// </summary>
    public string? Signature { get; init; }
}
