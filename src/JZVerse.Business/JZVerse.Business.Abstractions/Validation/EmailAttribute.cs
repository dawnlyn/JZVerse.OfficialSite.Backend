using System.Text.RegularExpressions;

namespace JZVerse.Business.Abstractions.Validation;

/// <summary>
/// 邮箱格式校验特性
/// </summary>
public sealed class EmailAttribute : ValidationAttribute
{
    // 邮箱正则表达式
    private static readonly Regex EmailRegex = new(
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public EmailAttribute()
    {
        ErrorMessage = "邮箱格式不正确";
    }

    public override bool IsValid(object? value)
    {
        // null 值由 Required 特性处理
        if (value is null)
            return true;

        if (value is not string str)
            return false;

        return EmailRegex.IsMatch(str);
    }
}
