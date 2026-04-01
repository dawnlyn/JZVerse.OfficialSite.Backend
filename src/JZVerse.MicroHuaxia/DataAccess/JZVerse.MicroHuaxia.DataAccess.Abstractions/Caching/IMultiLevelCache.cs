namespace JZVerse.MicroHuaxia.DataAccess.Abstractions.Caching;

/// <summary>
/// 多级缓存协调器接口
/// </summary>
/// <remarks>
/// 协调 L1(内存) → L2(Redis) → L3(数据库) 的读写流程
/// </remarks>
public interface IMultiLevelCache
{
    /// <summary>
    /// 获取或添加缓存
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="factory">缓存未命中时的数据加载委托（通常是数据库查询）</param>
    /// <param name="strategy">缓存策略（可选，使用默认策略）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>缓存值</returns>
    /// <remarks>
    /// 读取流程：L1 → L2 → factory(数据库)
    /// 命中时自动回填上层缓存
    /// </remarks>
    Task<T?> GetOrAddAsync<T>(
        string key,
        Func<Task<T?>> factory,
        CacheStrategyOptions? strategy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 设置缓存
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="value">缓存值</param>
    /// <param name="strategy">缓存策略（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SetAsync<T>(
        string key,
        T value,
        CacheStrategyOptions? strategy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 失效指定缓存键
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task InvalidateAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 失效指定实体的缓存
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="primaryKey">主键值</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task InvalidateByEntityAsync<TEntity>(object primaryKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// 失效指定实体类型的所有缓存
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="cancellationToken">取消令牌</param>
    Task InvalidateByEntityTypeAsync<TEntity>(CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量获取缓存
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="keys">缓存键列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>键值对字典</returns>
    Task<IDictionary<string, T?>> GetManyAsync<T>(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量设置缓存
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="items">键值对</param>
    /// <param name="strategy">缓存策略（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SetManyAsync<T>(
        IDictionary<string, T> items,
        CacheStrategyOptions? strategy = null,
        CancellationToken cancellationToken = default);
}
