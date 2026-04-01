using System.Data;

namespace JZVerse.MicroHuaxia.DataAccess.Abstractions;

/// <summary>
/// 数据库执行器接口，提供 Dapper 风格的 SQL 执行能力
/// </summary>
public interface IDbExecutor
{
    /// <summary>
    /// 执行查询并返回结果列表
    /// </summary>
    /// <typeparam name="T">结果类型</typeparam>
    /// <param name="sql">SQL 语句</param>
    /// <param name="param">参数对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>结果列表</returns>
    Task<IEnumerable<T>> QueryAsync<T>(
        string sql,
        object? param = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行查询并返回单个结果或默认值
    /// </summary>
    /// <typeparam name="T">结果类型</typeparam>
    /// <param name="sql">SQL 语句</param>
    /// <param name="param">参数对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>单个结果或默认值</returns>
    Task<T?> QuerySingleOrDefaultAsync<T>(
        string sql,
        object? param = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行查询并返回第一个结果或默认值
    /// </summary>
    /// <typeparam name="T">结果类型</typeparam>
    /// <param name="sql">SQL 语句</param>
    /// <param name="param">参数对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>第一个结果或默认值</returns>
    Task<T?> QueryFirstOrDefaultAsync<T>(
        string sql,
        object? param = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行非查询命令（INSERT、UPDATE、DELETE 等）
    /// </summary>
    /// <param name="sql">SQL 语句</param>
    /// <param name="param">参数对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>影响的行数</returns>
    Task<int> ExecuteAsync(
        string sql,
        object? param = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行查询并返回标量值
    /// </summary>
    /// <typeparam name="T">结果类型</typeparam>
    /// <param name="sql">SQL 语句</param>
    /// <param name="param">参数对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>标量值</returns>
    Task<T?> ExecuteScalarAsync<T>(
        string sql,
        object? param = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 在事务中执行操作
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="action">要执行的操作</param>
    /// <param name="isolationLevel">事务隔离级别</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<T> WithTransactionAsync<T>(
        Func<IDbTransaction, Task<T>> action,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 在事务中执行操作（无返回值）
    /// </summary>
    /// <param name="action">要执行的操作</param>
    /// <param name="isolationLevel">事务隔离级别</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task WithTransactionAsync(
        Func<IDbTransaction, Task> action,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 在指定事务中执行查询
    /// </summary>
    /// <typeparam name="T">结果类型</typeparam>
    /// <param name="sql">SQL 语句</param>
    /// <param name="param">参数对象</param>
    /// <param name="transaction">数据库事务</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>结果列表</returns>
    Task<IEnumerable<T>> QueryAsync<T>(
        string sql,
        object? param,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 在指定事务中执行非查询命令
    /// </summary>
    /// <param name="sql">SQL 语句</param>
    /// <param name="param">参数对象</param>
    /// <param name="transaction">数据库事务</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>影响的行数</returns>
    Task<int> ExecuteAsync(
        string sql,
        object? param,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 在指定事务中执行查询并返回标量值
    /// </summary>
    /// <typeparam name="T">结果类型</typeparam>
    /// <param name="sql">SQL 语句</param>
    /// <param name="param">参数对象</param>
    /// <param name="transaction">数据库事务</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>标量值</returns>
    Task<T?> ExecuteScalarAsync<T>(
        string sql,
        object? param,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);
}
