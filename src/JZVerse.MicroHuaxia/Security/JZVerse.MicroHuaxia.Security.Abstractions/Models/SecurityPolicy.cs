namespace JZVerse.MicroHuaxia.Security;

/// <summary>
/// 安全策略
/// </summary>
public sealed record SecurityPolicy
{
    /// <summary>
    /// 策略 ID
    /// </summary>
    public string PolicyId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 策略名称
    /// </summary>
    public string Name { get; init; } = null!;

    /// <summary>
    /// 策略描述
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 策略版本
    /// </summary>
    public string Version { get; init; } = "1.0";

    /// <summary>
    /// 策略效果
    /// </summary>
    public PolicyEffect Effect { get; init; }

    /// <summary>
    /// 策略优先级（数值越大优先级越高）
    /// </summary>
    public int Priority { get; init; } = 0;

    /// <summary>
    /// 命名空间
    /// </summary>
    public string? Namespace { get; init; }

    /// <summary>
    /// 主体匹配条件
    /// </summary>
    public IReadOnlyList<string> Subjects { get; init; } = new List<string>();

    /// <summary>
    /// 资源匹配条件
    /// </summary>
    public IReadOnlyList<string> Resources { get; init; } = new List<string>();

    /// <summary>
    /// 操作匹配条件
    /// </summary>
    public IReadOnlyList<string> Actions { get; init; } = new List<string>();

    /// <summary>
    /// 条件表达式（ABAC）
    /// </summary>
    public string? Condition { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; init; } = true;
}
