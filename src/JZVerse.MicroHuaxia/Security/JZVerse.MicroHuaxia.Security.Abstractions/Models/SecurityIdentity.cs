namespace JZVerse.MicroHuaxia.Security;

/// <summary>
/// 统一安全身份标识
/// </summary>
public sealed record SecurityIdentity
{
    /// <summary>
    /// 身份标识符
    /// </summary>
    public string IdentityId { get; init; } = null!;

    /// <summary>
    /// 身份类型
    /// </summary>
    public IdentityType Type { get; init; }

    /// <summary>
    /// 信任域
    /// </summary>
    public string TrustDomain { get; init; } = "cluster.local";

    /// <summary>
    /// 命名空间
    /// </summary>
    public string? Namespace { get; init; }

    /// <summary>
    /// 服务名称
    /// </summary>
    public string? ServiceName { get; init; }

    /// <summary>
    /// 声明集合
    /// </summary>
    public IReadOnlyDictionary<string, string> Claims { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// 角色列表
    /// </summary>
    public IReadOnlyList<string> Roles { get; init; } = new List<string>();

    /// <summary>
    /// 签发时间
    /// </summary>
    public DateTimeOffset IssuedAt { get; init; }

    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// 认证方式
    /// </summary>
    public AuthenticationMethod AuthenticationMethod { get; init; }

    /// <summary>
    /// 获取 SPIFFE ID 格式字符串
    /// </summary>
    public string ToSpiffeId()
    {
        if (Type == IdentityType.Service || Type == IdentityType.Workload)
        {
            return $"spiffe://{TrustDomain}/ns/{Namespace}/sa/{ServiceName}";
        }
        return IdentityId;
    }

    /// <summary>
    /// 检查身份是否已过期
    /// </summary>
    public bool IsExpired()
    {
        return DateTimeOffset.UtcNow > ExpiresAt;
    }

    /// <summary>
    /// 检查身份是否包含指定角色
    /// </summary>
    public bool HasRole(string role)
    {
        return Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 检查身份是否包含任意指定角色
    /// </summary>
    public bool HasAnyRole(params string[] roles)
    {
        return roles.Any(r => Roles.Contains(r, StringComparer.OrdinalIgnoreCase));
    }
}
