using System.Data;
using JZVerse.MicroHuaxia.DataAccess.Abstractions;
using JZVerse.MicroHuaxia.DataAccess.Abstractions.Models;
using JZVerse.MicroHuaxia.DataAccess.Core.Dialects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace JZVerse.MicroHuaxia.DataAccess.PostgreSql;

/// <summary>
/// PostgreSQL 连接工厂实现
/// </summary>
public sealed class PostgreSqlConnectionFactory : IDbConnectionFactory
{
    private readonly DbConnectionOptions _options;
    private readonly ILogger<PostgreSqlConnectionFactory> _logger;
    private readonly PostgreSqlDialect _dialect = new();

    /// <summary>
    /// 创建 PostgreSQL 连接工厂
    /// </summary>
    public PostgreSqlConnectionFactory(
        IOptions<DbConnectionOptions> options,
        ILogger<PostgreSqlConnectionFactory> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public DatabaseType DatabaseType => DatabaseType.PostgreSql;

    /// <inheritdoc />
    public IDbDialect Dialect => _dialect;

    /// <inheritdoc />
    public IDbConnection CreateConnection()
    {
        var builder = new NpgsqlConnectionStringBuilder(_options.ConnectionString)
        {
            Pooling = _options.EnablePooling,
            MinPoolSize = _options.MinPoolSize,
            MaxPoolSize = _options.MaxPoolSize,
            Timeout = _options.ConnectionTimeoutSeconds,
            CommandTimeout = _options.CommandTimeoutSeconds
        };

        return new NpgsqlConnection(builder.ConnectionString);
    }

    /// <inheritdoc />
    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = (NpgsqlConnection)CreateConnection();

        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open PostgreSQL connection");
            await connection.DisposeAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = (NpgsqlConnection)CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PostgreSQL connection test failed");
            return false;
        }
    }
}
