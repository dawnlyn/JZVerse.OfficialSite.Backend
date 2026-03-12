namespace JZVerse.Business.Abstractions.Validation;

/// <summary>
/// 必填校验特性
/// </summary>
public sealed class RequiredAttribute : ValidationAttribute
{
    /// <summary>
    /// 是否允许空白字符串（默认不允许）
    /// </summary>
    public bool AllowEmptyStrings { get; set; }

    public RequiredAttribute()
    {
        ErrorMessage = "该字段为必填项";
    }

    public override bool IsValid(object? value)
    {
        if (value is null)
            return false;

        if (value is string str && !AllowEmptyStrings)
            return !string.IsNullOrWhiteSpace(str);

        return true;
    }
}
