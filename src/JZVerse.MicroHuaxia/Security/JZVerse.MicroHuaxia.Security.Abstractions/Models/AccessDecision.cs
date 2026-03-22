namespace JZVerse.MicroHuaxia.Security;

/// <summary>
/// 访问决策结果
/// </summary>
public sealed record AccessDecision
{
    /// <summary>
    /// 是否允许访问
    /// </summary>
    public bool Allowed { get; init; }

    /// <summary>
    /// 决策原因
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// 匹配的策略列表
    /// </summary>
    public IReadOnlyList<string> MatchedPolicies { get; init; } = new List<string>();

    /// <summary>
    /// 扩展属性
    /// </summary>
    public IReadOnlyDictionary<string, object> Attributes { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// 决策时间
    /// </summary>
    public DateTimeOffset DecisionTime { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 决策耗时（毫秒）
    /// </summary>
    public long ElapsedMilliseconds { get; init; }

    /// <summary>
    /// 创建允许决策
    /// </summary>
    public static AccessDecision Allow(string reason, params string[] matchedPolicies)
    {
        return new AccessDecision
        {
            Allowed = true,
            Reason = reason,
            MatchedPolicies = matchedPolicies.ToList()
        };
    }

    /// <summary>
    /// 创建拒绝决策
    /// </summary>
    public static AccessDecision Deny(string reason)
    {
        return new AccessDecision
        {
            Allowed = false,
            Reason = reason,
            MatchedPolicies = new List<string>()
        };
    }

    /// <summary>
    /// 默认拒绝决策
    /// </summary>
    public static AccessDecision DefaultDeny { get; } = Deny("未匹配任何策略，默认拒绝访问");
}
