namespace JZVerse.MicroHuaxia.Security;

/// <summary>
/// 访问请求
/// </summary>
public sealed record AccessRequest
{
    /// <summary>
    /// 请求主体
    /// </summary>
    public SecurityIdentity Subject { get; init; } = null!;

    /// <summary>
    /// 目标资源
    /// </summary>
    public string Resource { get; init; } = null!;

    /// <summary>
    /// 操作类型
    /// </summary>
    public string Action { get; init; } = null!;

    /// <summary>
    /// 命名空间
    /// </summary>
    public string? Namespace { get; init; }

    /// <summary>
    /// 上下文信息
    /// </summary>
    public IReadOnlyDictionary<string, object> Context { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// 请求时间
    /// </summary>
    public DateTimeOffset RequestTime { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 请求 ID
    /// </summary>
    public string RequestId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 链路追踪 ID
    /// </summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// 创建简单的访问请求
    /// </summary>
    public static AccessRequest Create(
        SecurityIdentity subject,
        string resource,
        string action,
        string? ns = null)
    {
        return new AccessRequest
        {
            Subject = subject,
            Resource = resource,
            Action = action,
            Namespace = ns ?? subject.Namespace
        };
    }
}
