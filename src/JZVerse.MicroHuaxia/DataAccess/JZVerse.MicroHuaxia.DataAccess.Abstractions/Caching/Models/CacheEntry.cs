namespace JZVerse.MicroHuaxia.DataAccess.Abstractions.Caching;

/// <summary>
/// 缓存条目模型
/// </summary>
/// <typeparam name="T">缓存值类型</typeparam>
public sealed class CacheEntry<T>
{
    /// <summary>
    /// 缓存键
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// 缓存值
    /// </summary>
    public T? Value { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// 缓存来源级别
    /// </summary>
    public CacheLevel SourceLevel { get; init; }

    /// <summary>
    /// 是否已过期
    /// </summary>
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value <= DateTimeOffset.UtcNow;

    /// <summary>
    /// 是否为空值标记（防止缓存穿透）
    /// </summary>
    public bool IsNullMarker { get; init; }
}
