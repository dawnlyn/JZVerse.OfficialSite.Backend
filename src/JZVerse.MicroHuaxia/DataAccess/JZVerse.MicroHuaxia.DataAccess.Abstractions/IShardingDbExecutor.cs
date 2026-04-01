using System.Data;
using System.Linq.Expressions;
using JZVerse.MicroHuaxia.DataAccess.Abstractions.Sharding;

namespace JZVerse.MicroHuaxia.DataAccess.Abstractions;

/// <summary>
/// 支持分片的数据库执行器接口
/// </summary>
public interface IShardingDbExecutor : IDbExecutor
{
    /// <summary>
    /// 对指定实体类型执行查询，自动路由到对应的分片
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="shardingKey">分片键值</param>
    /// <param name="sql">SQL语句（使用逻辑表名）</param>
    /// <param name="param">参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>结果列表</returns>
    Task<IEnumerable<T>> QueryAsync<T>(
        object shardingKey,
        string sql,
        object? param = null,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// 对指定实体类型执行单条查询
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="shardingKey">分片键值</param>
    /// <param name="sql">SQL语句</param>
    /// <param name="param">参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>单个结果或默认值</returns>
    Task<T?> QuerySingleOrDefaultAsync<T>(
        object shardingKey,
        string sql,
        object? param = null,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// 对指定实体类型执行非查询命令
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="entity">实体对象（用于提取分片键）</param>
    /// <param name="sql">SQL语句</param>
    /// <param name="param">参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>影响的行数</returns>
    Task<int> ExecuteAsync<T>(
        T entity,
        string sql,
        object? param = null,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// 插入实体，自动路由到对应分片
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="entity">实体对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>影响的行数</returns>
    Task<int> InsertAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// 批量插入实体
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="entities">实体列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>影响的行数</returns>
    Task<int> InsertBatchAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// 更新实体
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="entity">实体对象</param>
    /// <param name="predicate">更新条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>影响的行数</returns>
    Task<int> UpdateAsync<T>(
        T entity,
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// 删除实体
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="entity">实体对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>影响的行数</returns>
    Task<int> DeleteAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// 根据条件删除
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="predicate">删除条件</param>
    /// <param name="shardingKey">分片键值</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>影响的行数</returns>
    Task<int> DeleteAsync<T>(
        Expression<Func<T, bool>> predicate,
        object shardingKey,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// 跨分片查询 - 在所有分片上执行查询并合并结果
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="sql">SQL语句</param>
    /// <param name="param">参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>合并后的结果列表</returns>
    Task<IEnumerable<T>> QueryAllShardsAsync<T>(
        string sql,
        object? param = null,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// 在指定事务中执行操作，自动路由
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <typeparam name="TResult">返回类型</typeparam>
    /// <param name="entity">实体对象（用于确定分片）</param>
    /// <param name="action">事务操作</param>
    /// <param name="isolationLevel">隔离级别</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<TResult> WithTransactionAsync<T, TResult>(
        T entity,
        Func<IDbTransaction, Task<TResult>> action,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// 获取当前路由上下文
    /// </summary>
    RoutingContext? GetCurrentRoutingContext();

    /// <summary>
    /// 手动设置路由上下文
    /// </summary>
    /// <param name="context">路由上下文</param>
    void SetRoutingContext(RoutingContext context);

    /// <summary>
    /// 清除当前路由上下文
    /// </summary>
    void ClearRoutingContext();
}
