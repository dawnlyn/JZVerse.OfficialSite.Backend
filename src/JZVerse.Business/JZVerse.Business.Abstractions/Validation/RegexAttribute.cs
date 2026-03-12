using System.Text.RegularExpressions;

namespace JZVerse.Business.Abstractions.Validation;

/// <summary>
/// 正则表达式校验特性
/// </summary>
public sealed class RegexAttribute : ValidationAttribute
{
    /// <summary>
    /// 正则表达式模式
    /// </summary>
    public string Pattern { get; }

    /// <summary>
    /// 正则表达式选项
    /// </summary>
    public RegexOptions Options { get; set; } = RegexOptions.None;

    private Regex? _regex;

    public RegexAttribute(string pattern)
    {
        Pattern = pattern;
        ErrorMessage = "格式不正确";
    }

    public override bool IsValid(object? value)
    {
        // null 值由 Required 特性处理
        if (value is null)
            return true;

        if (value is not string str)
            return false;

        _regex ??= new Regex(Pattern, Options);
        return _regex.IsMatch(str);
    }
}
