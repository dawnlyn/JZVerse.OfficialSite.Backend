using System.Text.RegularExpressions;

namespace JZVerse.Business.Abstractions.Validation;

/// <summary>
/// 手机号格式校验特性
/// </summary>
public sealed class PhoneAttribute : ValidationAttribute
{
    // 中国大陆手机号正则表达式
    private static readonly Regex PhoneRegex = new(
        @"^1[3-9]\d{9}$",
        RegexOptions.Compiled);

    public PhoneAttribute()
    {
        ErrorMessage = "手机号格式不正确";
    }

    public override bool IsValid(object? value)
    {
        // null 值由 Required 特性处理
        if (value is null)
            return true;

        if (value is not string str)
            return false;

        return PhoneRegex.IsMatch(str);
    }
}
