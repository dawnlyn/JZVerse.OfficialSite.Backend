namespace JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

/// <summary>
/// 配置事件类型
/// </summary>
public enum ConfigEventType
{
    /// <summary>
    /// 配置项变更
    /// </summary>
    ItemChanged = 0,

    /// <summary>
    /// 命名空间发布
    /// </summary>
    NamespacePublished = 1,

    /// <summary>
    /// 灰度发布开始
    /// </summary>
    GrayReleaseStarted = 2,

    /// <summary>
    /// 灰度发布完成
    /// </summary>
    GrayReleaseCompleted = 3,

    /// <summary>
    /// 配置回滚
    /// </summary>
    Rollback = 4,
}
