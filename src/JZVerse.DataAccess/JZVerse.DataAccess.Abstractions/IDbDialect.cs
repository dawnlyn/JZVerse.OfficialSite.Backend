using JZVerse.DataAccess.Abstractions.Models;

namespace JZVerse.DataAccess.Abstractions;

/// <summary>
/// 数据库方言接口，用于处理不同数据库的 SQL 语法差异
/// </summary>
public interface IDbDialect
{
    /// <summary>
    /// 数据库类型
    /// </summary>
    DatabaseType DatabaseType { get; }

    /// <summary>
    /// 参数前缀 (如 @ 或 :)
    /// </summary>
    string ParameterPrefix { get; }

    /// <summary>
    /// 引用标识符（表名、列名等）
    /// </summary>
    /// <param name="identifier">标识符</param>
    /// <returns>引用后的标识符</returns>
    string QuoteIdentifier(string identifier);

    /// <summary>
    /// 构建分页查询
    /// </summary>
    /// <param name="sql">原始 SQL</param>
    /// <param name="offset">偏移量</param>
    /// <param name="limit">限制数量</param>
    /// <returns>分页 SQL</returns>
    string BuildPagedQuery(string sql, int offset, int limit);

    /// <summary>
    /// 获取最后插入 ID 的命令
    /// </summary>
    /// <returns>SQL 命令</returns>
    string GetLastInsertIdCommand();

    /// <summary>
    /// 是否支持 RETURNING 子句
    /// </summary>
    bool SupportsReturning { get; }

    /// <summary>
    /// 构建带 RETURNING 的插入语句
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <param name="columns">列名列表</param>
    /// <param name="returningColumn">返回的列名</param>
    /// <returns>SQL 语句</returns>
    string BuildInsertReturning(string tableName, IEnumerable<string> columns, string returningColumn);

    /// <summary>
    /// 获取当前时间戳的 SQL 函数
    /// </summary>
    /// <returns>时间戳函数</returns>
    string GetCurrentTimestampFunction();

    /// <summary>
    /// 格式化布尔值
    /// </summary>
    /// <param name="value">布尔值</param>
    /// <returns>SQL 格式的布尔值</returns>
    string FormatBoolean(bool value);

    /// <summary>
    /// 获取 UUID/GUID 类型的 SQL 类型名称
    /// </summary>
    /// <returns>类型名称</returns>
    string GetGuidTypeName();
}
