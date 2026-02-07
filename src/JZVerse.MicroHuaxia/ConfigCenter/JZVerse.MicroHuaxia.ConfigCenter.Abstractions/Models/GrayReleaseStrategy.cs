namespace JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

/// <summary>
/// 灰度发布策略
/// </summary>
public enum GrayReleaseStrategy
{
    /// <summary>
    /// 手动灰度
    /// </summary>
    Manual = 0,

    /// <summary>
    /// 自动灰度
    /// </summary>
    Automatic = 1,

    /// <summary>
    /// 金丝雀发布
    /// </summary>
    Canary = 2,
}
