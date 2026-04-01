using System.Data;
using JZVerse.MicroHuaxia.DataAccess.Abstractions;
using JZVerse.MicroHuaxia.DataAccess.Abstractions.Models;
using JZVerse.MicroHuaxia.DataAccess.Core.Dialects;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.DataAccess.Sqlite;

/// <summary>
/// SQLite 连接工厂实现
/// </summary>
public sealed class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly DbConnectionOptions _options;
    private readonly ILogger<SqliteConnectionFactory> _logger;
    private readonly SqliteDialect _dialect = new();

    /// <summary>
    /// 创建 SQLite 连接工厂
    /// </summary>
    public SqliteConnectionFactory(
        IOptions<DbConnectionOptions> options,
        ILogger<SqliteConnectionFactory> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public DatabaseType DatabaseType => DatabaseType.Sqlite;

    /// <inheritdoc />
    public IDbDialect Dialect => _dialect;

    /// <inheritdoc />
    public IDbConnection CreateConnection()
    {
        var builder = new SqliteConnectionStringBuilder(_options.ConnectionString)
        {
            Pooling = _options.EnablePooling,
            DefaultTimeout = _options.ConnectionTimeoutSeconds
        };

        return new SqliteConnection(builder.ConnectionString);
    }

    /// <inheritdoc />
    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = (SqliteConnection)CreateConnection();

        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open SQLite connection");
            await connection.DisposeAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = (SqliteConnection)CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqliteCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SQLite connection test failed");
            return false;
        }
    }
}
