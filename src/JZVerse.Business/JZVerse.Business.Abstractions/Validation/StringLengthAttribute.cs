namespace JZVerse.Business.Abstractions.Validation;

/// <summary>
/// 字符串长度校验特性
/// </summary>
public sealed class StringLengthAttribute : ValidationAttribute
{
    /// <summary>
    /// 最小长度
    /// </summary>
    public int MinLength { get; set; }

    /// <summary>
    /// 最大长度
    /// </summary>
    public int MaxLength { get; set; } = int.MaxValue;

    public StringLengthAttribute()
    {
        ErrorMessage = "字符串长度不符合要求";
    }

    public StringLengthAttribute(int maxLength)
    {
        MaxLength = maxLength;
        ErrorMessage = $"字符串长度不能超过 {maxLength}";
    }

    public override bool IsValid(object? value)
    {
        // null 值由 Required 特性处理
        if (value is null)
            return true;

        if (value is not string str)
            return false;

        var length = str.Length;
        return length >= MinLength && length <= MaxLength;
    }

    public override string FormatErrorMessage(string propertyName)
    {
        if (ErrorMessage != "字符串长度不符合要求" && ErrorMessage != $"字符串长度不能超过 {MaxLength}")
            return ErrorMessage;

        if (MinLength > 0 && MaxLength < int.MaxValue)
            return $"{propertyName} 的长度必须在 {MinLength} 到 {MaxLength} 之间";

        if (MinLength > 0)
            return $"{propertyName} 的长度不能少于 {MinLength}";

        return $"{propertyName} 的长度不能超过 {MaxLength}";
    }
}
