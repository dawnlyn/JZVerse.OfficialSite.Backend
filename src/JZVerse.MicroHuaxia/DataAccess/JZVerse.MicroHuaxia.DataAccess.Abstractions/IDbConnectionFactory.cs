using System.Data;
using JZVerse.MicroHuaxia.DataAccess.Abstractions.Models;

namespace JZVerse.MicroHuaxia.DataAccess.Abstractions;

/// <summary>
/// 数据库连接工厂接口
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>
    /// 数据库类型
    /// </summary>
    DatabaseType DatabaseType { get; }

    /// <summary>
    /// 获取数据库方言
    /// </summary>
    IDbDialect Dialect { get; }

    /// <summary>
    /// 创建数据库连接
    /// </summary>
    /// <returns>数据库连接</returns>
    IDbConnection CreateConnection();

    /// <summary>
    /// 异步创建并打开数据库连接
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已打开的数据库连接</returns>
    Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 测试数据库连接
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>连接是否成功</returns>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}
