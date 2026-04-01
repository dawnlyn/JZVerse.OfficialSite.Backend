namespace JZVerse.MicroHuaxia.DataAccess.Abstractions.Caching;

/// <summary>
/// 缓存策略选项
/// </summary>
public sealed class CacheStrategyOptions
{
    /// <summary>
    /// 启用的缓存级别
    /// </summary>
    public CacheLevel Levels { get; set; } = CacheLevel.Memory | CacheLevel.Redis;

    /// <summary>
    /// 过期时间（秒），-1 表示永不过期
    /// </summary>
    public int ExpirationSeconds { get; set; } = 600;

    /// <summary>
    /// 是否缓存空值（防止缓存穿透）
    /// </summary>
    public bool CacheNullValue { get; set; } = true;

    /// <summary>
    /// 空值缓存过期时间（秒）
    /// </summary>
    public int NullValueExpirationSeconds { get; set; } = 60;

    /// <summary>
    /// 是否启用过期时间抖动（防止缓存雪崩）
    /// </summary>
    public bool EnableExpirationJitter { get; set; } = true;

    /// <summary>
    /// 过期时间抖动比例（0-1）
    /// </summary>
    public double ExpirationJitterRatio { get; set; } = 0.1;

    /// <summary>
    /// 获取过期时间（带抖动）
    /// </summary>
    public TimeSpan? GetExpiration()
    {
        if (ExpirationSeconds < 0)
            return null;

        var baseSeconds = ExpirationSeconds;

        if (EnableExpirationJitter && ExpirationJitterRatio > 0)
        {
            var jitter = (int)(baseSeconds * ExpirationJitterRatio);
            baseSeconds += Random.Shared.Next(-jitter, jitter + 1);
        }

        return TimeSpan.FromSeconds(Math.Max(1, baseSeconds));
    }

    /// <summary>
    /// 获取空值过期时间
    /// </summary>
    public TimeSpan GetNullValueExpiration()
    {
        return TimeSpan.FromSeconds(Math.Max(1, NullValueExpirationSeconds));
    }
}
