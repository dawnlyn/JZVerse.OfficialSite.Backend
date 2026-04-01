namespace JZVerse.MicroHuaxia.DataAccess.Abstractions.Caching;

/// <summary>
/// 单级缓存提供者接口
/// </summary>
/// <remarks>
/// 每个缓存级别（内存、Redis）都实现此接口
/// </remarks>
public interface ICacheProvider
{
    /// <summary>
    /// 缓存级别
    /// </summary>
    CacheLevel Level { get; }

    /// <summary>
    /// 缓存是否可用
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// 获取缓存值
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>缓存值，不存在返回 null</returns>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 设置缓存值
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="value">缓存值</param>
    /// <param name="expiration">过期时间，null 表示永不过期</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 移除缓存
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查缓存是否存在
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否存在</returns>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按前缀移除缓存
    /// </summary>
    /// <param name="prefix">缓存键前缀</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清空所有缓存
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
