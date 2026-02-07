namespace JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

/// <summary>
/// 配置变更类型
/// </summary>
public enum ConfigChangeType
{
    /// <summary>
    /// 新建
    /// </summary>
    Created = 0,

    /// <summary>
    /// 更新
    /// </summary>
    Updated = 1,

    /// <summary>
    /// 删除
    /// </summary>
    Deleted = 2,

    /// <summary>
    /// 回滚
    /// </summary>
    Rollback = 3,
}
