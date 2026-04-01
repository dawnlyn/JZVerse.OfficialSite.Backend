using JZVerse.MicroHuaxia.DataAccess.Abstractions;
using JZVerse.MicroHuaxia.DataAccess.Abstractions.Models;

namespace JZVerse.MicroHuaxia.DataAccess.Core.Dialects;

/// <summary>
/// MongoDB 数据库方言
/// </summary>
/// <remarks>
/// MongoDB 不是关系型数据库，不使用 SQL。
/// 此方言主要用于统一接口，实际操作需使用 MongoDB 驱动 API。
/// </remarks>
public sealed class MongoDbDialect : IDbDialect
{
    /// <inheritdoc />
    public DatabaseType DatabaseType => DatabaseType.MongoDB;

    /// <inheritdoc />
    public string ParameterPrefix => string.Empty;

    /// <inheritdoc />
    public string QuoteIdentifier(string identifier)
    {
        // MongoDB 使用字段名，不需要引用
        return identifier;
    }

    /// <inheritdoc />
    public string BuildPagedQuery(string sql, int offset, int limit)
    {
        // MongoDB 使用 skip/limit，此方法不适用
        throw new NotSupportedException("MongoDB does not use SQL. Use MongoDB driver's Skip() and Limit() methods instead.");
    }

    /// <inheritdoc />
    public string GetLastInsertIdCommand()
    {
        throw new NotSupportedException("MongoDB does not support LAST_INSERT_ID. Use the insertedId from insert result.");
    }

    /// <inheritdoc />
    public bool SupportsReturning => false;

    /// <inheritdoc />
    public string BuildInsertReturning(string tableName, IEnumerable<string> columns, string returningColumn)
    {
        throw new NotSupportedException("MongoDB does not use SQL INSERT statements.");
    }

    /// <inheritdoc />
    public string GetCurrentTimestampFunction()
    {
        // MongoDB 使用 new Date() 或 ISODate()
        return "new Date()";
    }

    /// <inheritdoc />
    public string FormatBoolean(bool value)
    {
        return value ? "true" : "false";
    }

    /// <inheritdoc />
    public string GetGuidTypeName()
    {
        // MongoDB 原生支持 UUID
        return "UUID";
    }
}
