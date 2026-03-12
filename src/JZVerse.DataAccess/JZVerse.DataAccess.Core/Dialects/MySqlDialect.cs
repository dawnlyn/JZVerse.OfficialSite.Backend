using JZVerse.DataAccess.Abstractions;
using JZVerse.DataAccess.Abstractions.Models;

namespace JZVerse.DataAccess.Core.Dialects;

/// <summary>
/// MySQL 数据库方言
/// </summary>
public sealed class MySqlDialect : IDbDialect
{
    /// <inheritdoc />
    public DatabaseType DatabaseType => DatabaseType.MySql;

    /// <inheritdoc />
    public string ParameterPrefix => "@";

    /// <inheritdoc />
    public string QuoteIdentifier(string identifier)
    {
        return $"`{identifier.Replace("`", "``")}`";
    }

    /// <inheritdoc />
    public string BuildPagedQuery(string sql, int offset, int limit)
    {
        return $"{sql} LIMIT {limit} OFFSET {offset}";
    }

    /// <inheritdoc />
    public string GetLastInsertIdCommand()
    {
        return "SELECT LAST_INSERT_ID()";
    }

    /// <inheritdoc />
    public bool SupportsReturning => false;

    /// <inheritdoc />
    public string BuildInsertReturning(string tableName, IEnumerable<string> columns, string returningColumn)
    {
        var columnList = string.Join(", ", columns.Select(QuoteIdentifier));
        var paramList = string.Join(", ", columns.Select(c => $"@{c}"));
        return $"INSERT INTO {QuoteIdentifier(tableName)} ({columnList}) VALUES ({paramList}); SELECT LAST_INSERT_ID();";
    }

    /// <inheritdoc />
    public string GetCurrentTimestampFunction()
    {
        return "NOW()";
    }

    /// <inheritdoc />
    public string FormatBoolean(bool value)
    {
        return value ? "1" : "0";
    }

    /// <inheritdoc />
    public string GetGuidTypeName()
    {
        return "BINARY(16)";
    }
}
