namespace JZVerse.Business.Abstractions.Validation;

/// <summary>
/// 数值范围校验特性
/// </summary>
public sealed class RangeAttribute : ValidationAttribute
{
    /// <summary>
    /// 最小值
    /// </summary>
    public object Minimum { get; }

    /// <summary>
    /// 最大值
    /// </summary>
    public object Maximum { get; }

    /// <summary>
    /// 值类型
    /// </summary>
    public Type OperandType { get; }

    public RangeAttribute(int minimum, int maximum)
    {
        Minimum = minimum;
        Maximum = maximum;
        OperandType = typeof(int);
        ErrorMessage = $"值必须在 {minimum} 到 {maximum} 之间";
    }

    public RangeAttribute(double minimum, double maximum)
    {
        Minimum = minimum;
        Maximum = maximum;
        OperandType = typeof(double);
        ErrorMessage = $"值必须在 {minimum} 到 {maximum} 之间";
    }

    public RangeAttribute(Type type, string minimum, string maximum)
    {
        OperandType = type;
        Minimum = minimum;
        Maximum = maximum;
        ErrorMessage = $"值必须在 {minimum} 到 {maximum} 之间";
    }

    public override bool IsValid(object? value)
    {
        // null 值由 Required 特性处理
        if (value is null)
            return true;

        try
        {
            var convertedValue = Convert.ToDouble(value);
            var min = Convert.ToDouble(Minimum);
            var max = Convert.ToDouble(Maximum);

            return convertedValue >= min && convertedValue <= max;
        }
        catch
        {
            return false;
        }
    }
}
