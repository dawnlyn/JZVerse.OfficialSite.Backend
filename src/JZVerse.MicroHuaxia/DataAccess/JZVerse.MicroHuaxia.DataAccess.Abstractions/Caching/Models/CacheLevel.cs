namespace JZVerse.MicroHuaxia.DataAccess.Abstractions.Caching;

/// <summary>
/// 缓存级别枚举（支持位标志组合）
/// </summary>
[Flags]
public enum CacheLevel
{
    /// <summary>
    /// 无缓存
    /// </summary>
    None = 0,

    /// <summary>
    /// L1 内存缓存
    /// </summary>
    Memory = 1 << 0,

    /// <summary>
    /// L2 Redis/Garnet 分布式缓存
    /// </summary>
    Redis = 1 << 1,

    /// <summary>
    /// L1 + L2 组合
    /// </summary>
    All = Memory | Redis
}
