namespace JZVerse.Business.Abstractions.Validation;

/// <summary>
/// 参数校验特性基类
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public abstract class ValidationAttribute : Attribute
{
    /// <summary>
    /// 错误消息，默认为"未知错误"
    /// </summary>
    public string ErrorMessage { get; set; } = "未知错误";

    /// <summary>
    /// 校验值是否有效
    /// </summary>
    /// <param name="value">待校验的值</param>
    /// <returns>是否有效</returns>
    public abstract bool IsValid(object? value);

    /// <summary>
    /// 获取格式化后的错误消息
    /// </summary>
    /// <param name="propertyName">属性名称</param>
    /// <returns>格式化后的错误消息</returns>
    public virtual string FormatErrorMessage(string propertyName)
    {
        return ErrorMessage;
    }
}
