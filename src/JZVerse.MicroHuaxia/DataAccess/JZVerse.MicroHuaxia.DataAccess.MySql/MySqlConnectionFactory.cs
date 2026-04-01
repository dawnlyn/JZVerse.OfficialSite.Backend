using System.Data;
using JZVerse.MicroHuaxia.DataAccess.Abstractions;
using JZVerse.MicroHuaxia.DataAccess.Abstractions.Models;
using JZVerse.MicroHuaxia.DataAccess.Core.Dialects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace JZVerse.MicroHuaxia.DataAccess.MySql;

/// <summary>
/// MySQL 连接工厂实现
/// </summary>
public sealed class MySqlConnectionFactory : IDbConnectionFactory
{
    private readonly DbConnectionOptions _options;
    private readonly ILogger<MySqlConnectionFactory> _logger;
    private readonly MySqlDialect _dialect = new();

    /// <summary>
    /// 创建 MySQL 连接工厂
    /// </summary>
    public MySqlConnectionFactory(
        IOptions<DbConnectionOptions> options,
        ILogger<MySqlConnectionFactory> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public DatabaseType DatabaseType => DatabaseType.MySql;

    /// <inheritdoc />
    public IDbDialect Dialect => _dialect;

    /// <inheritdoc />
    public IDbConnection CreateConnection()
    {
        var builder = new MySqlConnectionStringBuilder(_options.ConnectionString)
        {
            Pooling = _options.EnablePooling,
            MinimumPoolSize = (uint)_options.MinPoolSize,
            MaximumPoolSize = (uint)_options.MaxPoolSize,
            ConnectionTimeout = (uint)_options.ConnectionTimeoutSeconds,
            DefaultCommandTimeout = (uint)_options.CommandTimeoutSeconds
        };

        return new MySqlConnection(builder.ConnectionString);
    }

    /// <inheritdoc />
    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = (MySqlConnection)CreateConnection();

        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open MySQL connection");
            await connection.DisposeAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = (MySqlConnection)CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MySQL connection test failed");
            return false;
        }
    }
}
