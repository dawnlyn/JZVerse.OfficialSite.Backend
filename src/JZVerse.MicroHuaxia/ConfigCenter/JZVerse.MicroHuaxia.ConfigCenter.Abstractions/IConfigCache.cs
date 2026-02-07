namespace JZVerse.MicroHuaxia.ConfigCenter.Abstractions;

/// <summary>
/// 配置缓存接口
/// </summary>
public interface IConfigCache
{
    /// <summary>
    /// 是否启用缓存
    /// </summary>
    bool IsCacheEnabled { get; }

    /// <summary>
    /// 获取缓存的配置
    /// </summary>
    Task<Dictionary<string, string>?> GetAsync(
        string cacheKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 设置缓存
    /// </summary>
    Task SetAsync(
        string cacheKey,
        Dictionary<string, string> config,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 使缓存失效
    /// </summary>
    Task InvalidateAsync(
        string cacheKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 按前缀使缓存失效
    /// </summary>
    Task InvalidateByPrefixAsync(
        string prefix,
        CancellationToken cancellationToken = default);
}
