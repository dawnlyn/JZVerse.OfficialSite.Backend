namespace JZVerse.DataAccess.Abstractions.Caching;

/// <summary>
/// 缓存键生成器接口
/// </summary>
public interface ICacheKeyGenerator
{
    /// <summary>
    /// 生成实体缓存键
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="primaryKey">主键值（可选，为 null 时生成整表缓存键）</param>
    /// <returns>缓存键</returns>
    string GenerateKey<TEntity>(object? primaryKey = null);

    /// <summary>
    /// 生成实体缓存键
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <param name="primaryKey">主键值（可选）</param>
    /// <returns>缓存键</returns>
    string GenerateKey(Type entityType, object? primaryKey = null);

    /// <summary>
    /// 生成查询缓存键
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="queryIdentifier">查询标识符</param>
    /// <param name="parameters">查询参数（可选）</param>
    /// <returns>缓存键</returns>
    string GenerateQueryKey<TEntity>(string queryIdentifier, object? parameters = null);

    /// <summary>
    /// 生成实体类型的前缀
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <returns>缓存键前缀</returns>
    string GenerateEntityPrefix<TEntity>();

    /// <summary>
    /// 生成实体类型的前缀
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>缓存键前缀</returns>
    string GenerateEntityPrefix(Type entityType);
}
