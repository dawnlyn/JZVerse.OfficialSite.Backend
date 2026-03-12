using JZVerse.DataAccess.Abstractions;
using JZVerse.DataAccess.Abstractions.Models;

namespace JZVerse.DataAccess.Core.Dialects;

/// <summary>
/// SQLite 数据库方言
/// </summary>
public sealed class SqliteDialect : IDbDialect
{
    /// <inheritdoc />
    public DatabaseType DatabaseType => DatabaseType.Sqlite;

    /// <inheritdoc />
    public string ParameterPrefix => "@";

    /// <inheritdoc />
    public string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"")}\"";
    }

    /// <inheritdoc />
    public string BuildPagedQuery(string sql, int offset, int limit)
    {
        return $"{sql} LIMIT {limit} OFFSET {offset}";
    }

    /// <inheritdoc />
    public string GetLastInsertIdCommand()
    {
        return "SELECT last_insert_rowid()";
    }

    /// <inheritdoc />
    public bool SupportsReturning => true;

    /// <inheritdoc />
    public string BuildInsertReturning(string tableName, IEnumerable<string> columns, string returningColumn)
    {
        var columnList = string.Join(", ", columns.Select(QuoteIdentifier));
        var paramList = string.Join(", ", columns.Select(c => $"@{c}"));
        return $"INSERT INTO {QuoteIdentifier(tableName)} ({columnList}) VALUES ({paramList}) RETURNING {QuoteIdentifier(returningColumn)}";
    }

    /// <inheritdoc />
    public string GetCurrentTimestampFunction()
    {
        return "datetime('now')";
    }

    /// <inheritdoc />
    public string FormatBoolean(bool value)
    {
        return value ? "1" : "0";
    }

    /// <inheritdoc />
    public string GetGuidTypeName()
    {
        return "BLOB";
    }
}
