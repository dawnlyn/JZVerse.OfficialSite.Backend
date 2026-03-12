using System.Data;
using Dapper;
using JZVerse.DataAccess.Abstractions;
using Microsoft.Extensions.Logging;

namespace JZVerse.DataAccess.Core.Executors;

/// <summary>
/// Dapper 风格的数据库执行器实现
/// </summary>
public sealed class DapperExecutor : IDbExecutor
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<DapperExecutor> _logger;
    private readonly int _commandTimeout;

    /// <summary>
    /// 创建 Dapper 执行器
    /// </summary>
    public DapperExecutor(
        IDbConnectionFactory connectionFactory,
        ILogger<DapperExecutor> logger,
        int commandTimeout = 30)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
        _commandTimeout = commandTimeout;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> QueryAsync<T>(
        string sql,
        object? param = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (await _connectionFactory.CreateOpenConnectionAsync(cancellationToken) as IAsyncDisposable)
            ?? throw new InvalidOperationException("Connection does not support async dispose");

        var dbConnection = (IDbConnection)connection;
        return await dbConnection.QueryAsync<T>(
            new CommandDefinition(sql, param, commandTimeout: _commandTimeout, cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public async Task<T?> QuerySingleOrDefaultAsync<T>(
        string sql,
        object? param = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (await _connectionFactory.CreateOpenConnectionAsync(cancellationToken) as IAsyncDisposable)
            ?? throw new InvalidOperationException("Connection does not support async dispose");

        var dbConnection = (IDbConnection)connection;
        return await dbConnection.QuerySingleOrDefaultAsync<T>(
            new CommandDefinition(sql, param, commandTimeout: _commandTimeout, cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public async Task<T?> QueryFirstOrDefaultAsync<T>(
        string sql,
        object? param = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (await _connectionFactory.CreateOpenConnectionAsync(cancellationToken) as IAsyncDisposable)
            ?? throw new InvalidOperationException("Connection does not support async dispose");

        var dbConnection = (IDbConnection)connection;
        return await dbConnection.QueryFirstOrDefaultAsync<T>(
            new CommandDefinition(sql, param, commandTimeout: _commandTimeout, cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(
        string sql,
        object? param = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (await _connectionFactory.CreateOpenConnectionAsync(cancellationToken) as IAsyncDisposable)
            ?? throw new InvalidOperationException("Connection does not support async dispose");

        var dbConnection = (IDbConnection)connection;
        return await dbConnection.ExecuteAsync(
            new CommandDefinition(sql, param, commandTimeout: _commandTimeout, cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public async Task<T?> ExecuteScalarAsync<T>(
        string sql,
        object? param = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (await _connectionFactory.CreateOpenConnectionAsync(cancellationToken) as IAsyncDisposable)
            ?? throw new InvalidOperationException("Connection does not support async dispose");

        var dbConnection = (IDbConnection)connection;
        return await dbConnection.ExecuteScalarAsync<T>(
            new CommandDefinition(sql, param, commandTimeout: _commandTimeout, cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public async Task<T> WithTransactionAsync<T>(
        Func<IDbTransaction, Task<T>> action,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        try
        {
            using var transaction = connection.BeginTransaction(isolationLevel);

            try
            {
                var result = await action(transaction);
                transaction.Commit();
                return result;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        finally
        {
            if (connection is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            else
                connection.Dispose();
        }
    }

    /// <inheritdoc />
    public async Task WithTransactionAsync(
        Func<IDbTransaction, Task> action,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        await WithTransactionAsync(async tx =>
        {
            await action(tx);
            return 0;
        }, isolationLevel, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> QueryAsync<T>(
        string sql,
        object? param,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        var connection = transaction.Connection
            ?? throw new InvalidOperationException("Transaction has no associated connection");

        return await connection.QueryAsync<T>(
            new CommandDefinition(sql, param, transaction, commandTimeout: _commandTimeout, cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(
        string sql,
        object? param,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        var connection = transaction.Connection
            ?? throw new InvalidOperationException("Transaction has no associated connection");

        return await connection.ExecuteAsync(
            new CommandDefinition(sql, param, transaction, commandTimeout: _commandTimeout, cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public async Task<T?> ExecuteScalarAsync<T>(
        string sql,
        object? param,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        var connection = transaction.Connection
            ?? throw new InvalidOperationException("Transaction has no associated connection");

        return await connection.ExecuteScalarAsync<T>(
            new CommandDefinition(sql, param, transaction, commandTimeout: _commandTimeout, cancellationToken: cancellationToken));
    }
}
