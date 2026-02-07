namespace JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

/// <summary>
/// 配置值类型
/// </summary>
public enum ConfigValueType
{
    /// <summary>
    /// 字符串
    /// </summary>
    String = 0,

    /// <summary>
    /// 整数
    /// </summary>
    Int = 1,

    /// <summary>
    /// 长整数
    /// </summary>
    Long = 2,

    /// <summary>
    /// 双精度浮点数
    /// </summary>
    Double = 3,

    /// <summary>
    /// 布尔值
    /// </summary>
    Boolean = 4,

    /// <summary>
    /// JSON 对象
    /// </summary>
    JSON = 5,

    /// <summary>
    /// 数组
    /// </summary>
    Array = 6,
}
