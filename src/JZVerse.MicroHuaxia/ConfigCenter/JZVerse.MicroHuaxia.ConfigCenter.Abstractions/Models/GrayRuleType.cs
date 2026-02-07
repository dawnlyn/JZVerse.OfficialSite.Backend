namespace JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

/// <summary>
/// 灰度规则类型
/// </summary>
public enum GrayRuleType
{
    /// <summary>
    /// IP 地址匹配
    /// </summary>
    IP = 0,

    /// <summary>
    /// 标签匹配
    /// </summary>
    Tag = 1,

    /// <summary>
    /// 客户端 ID 匹配
    /// </summary>
    ClientId = 2,

    /// <summary>
    /// 百分比灰度
    /// </summary>
    Percentage = 3,
}
