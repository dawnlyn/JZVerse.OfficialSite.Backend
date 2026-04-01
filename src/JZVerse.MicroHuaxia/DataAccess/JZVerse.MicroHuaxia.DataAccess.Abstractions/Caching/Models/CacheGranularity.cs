namespace JZVerse.MicroHuaxia.DataAccess.Abstractions.Caching;

/// <summary>
/// 缓存粒度枚举
/// </summary>
public enum CacheGranularity
{
    /// <summary>
    /// 按主键缓存单条记录
    /// </summary>
    ByPrimaryKey,

    /// <summary>
    /// 整表缓存所有记录
    /// </summary>
    TableLevel
}
