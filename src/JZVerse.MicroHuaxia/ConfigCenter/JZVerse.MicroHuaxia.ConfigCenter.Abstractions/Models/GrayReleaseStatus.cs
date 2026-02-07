namespace JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

/// <summary>
/// 灰度发布状态
/// </summary>
public enum GrayReleaseStatus
{
    /// <summary>
    /// 草稿
    /// </summary>
    Draft = 0,

    /// <summary>
    /// 进行中
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// 已完成
    /// </summary>
    Completed = 2,

    /// <summary>
    /// 已回滚
    /// </summary>
    Rollback = 3,

    /// <summary>
    /// 已取消
    /// </summary>
    Cancelled = 4,
}
