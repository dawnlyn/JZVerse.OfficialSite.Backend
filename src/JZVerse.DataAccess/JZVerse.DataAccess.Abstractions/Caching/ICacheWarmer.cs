namespace JZVerse.DataAccess.Abstractions.Caching;

/// <summary>
/// 缓存预热服务接口
/// </summary>
public interface ICacheWarmer
{
    /// <summary>
    /// 执行所有标记实体的缓存预热
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task WarmupAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 预热指定实体类型的缓存
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="cancellationToken">取消令牌</param>
    Task WarmupEntityAsync<TEntity>(CancellationToken cancellationToken = default);

    /// <summary>
    /// 预热指定实体类型的缓存
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task WarmupEntityAsync(Type entityType, CancellationToken cancellationToken = default);
}
