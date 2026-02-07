namespace JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

/// <summary>
/// 灰度规则
/// </summary>
public sealed class GrayReleaseRule
{
    /// <summary>
    /// 规则类型
    /// </summary>
    public GrayRuleType RuleType { get; init; }

    /// <summary>
    /// 匹配模式
    /// </summary>
    public required string MatchPattern { get; init; }

    /// <summary>
    /// 优先级（数字越小优先级越高）
    /// </summary>
    public int Priority { get; init; }
}
