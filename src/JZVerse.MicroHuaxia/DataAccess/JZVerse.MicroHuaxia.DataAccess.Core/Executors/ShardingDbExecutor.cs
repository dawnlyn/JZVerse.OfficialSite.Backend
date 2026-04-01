using System.Data;
using System.Linq.Expressions;
using System.Text;
using Dapper;
using JZVerse.MicroHuaxia.DataAccess.Abstractions;
using JZVerse.MicroHuaxia.DataAccess.Abstractions.Sharding;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.DataAccess.Core.Executors;

/// <summary>
/// 支持分片的数据库执行器实现
/// </summary>
public class ShardingDbExecutor : IShardingDbExecutor
{
    private readonly IMultiDbConnectionManager _connectionManager;
    private readonly IDatabaseRouter _router;
    private readonly IEntityMetadataProvider _metadataProvider;
    private readonly ILogger<ShardingDbExecutor> _logger;
    private readonly int _commandTimeout;

    private readonly AsyncLocal<RoutingContext?> _currentRoutingContext = new();

    public ShardingDbExecutor(
        IMultiDbConnectionManager connectionManager,
        IDatabaseRouter router,
        IEntityMetadataProvider metadataProvider,
        ILogger<ShardingDbExecutor> logger,
        int commandTimeout = 30)
    {
        _connectionManager = connectionManager;
        _router = router;
        _metadataProvider = metadataProvider;
        _logger = logger;
        _commandTimeout = commandTimeout;
    }

    #region IDbExecutor Implementation (Non-sharding methods)

    public Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default)
    {
        // 如果设置了路由上下文，使用它
        if (_currentRoutingContext.Value != null)
        {
            return QueryWithContextAsync<T>(_currentRoutingContext.Value, sql, param, cancellationToken);
        }

        // 否则使用默认连接
        throw new InvalidOperationException(
            "ShardingDbExecutor requires a routing context for non-typed queries. " +
            "Use QueryAsync<T>(shardingKey, sql, ...) or SetRoutingContext() instead.");
    }

    public Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default)
    {
        if (_currentRoutingContext.Value != null)
        {
            return QuerySingleOrDefaultWithContextAsync<T>(_currentRoutingContext.Value, sql, param, cancellationToken);
        }

        throw new InvalidOperationException(
            "ShardingDbExecutor requires a routing context for non-typed queries. " +
            "Use QuerySingleOrDefaultAsync<T>(shardingKey, sql, ...) or SetRoutingContext() instead.");
    }

    public Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default)
    {
        if (_currentRoutingContext.Value != null)
        {
            return QueryFirstOrDefaultWithContextAsync<T>(_currentRoutingContext.Value, sql, param, cancellationToken);
        }

        throw new InvalidOperationException(
            "ShardingDbExecutor requires a routing context for non-typed queries.");
    }

    public Task<int> ExecuteAsync(string sql, object? param = null, CancellationToken cancellationToken = default)
    {
        if (_currentRoutingContext.Value != null)
        {
            return ExecuteWithContextAsync(_currentRoutingContext.Value, sql, param, cancellationToken);
        }

        throw new InvalidOperationException(
            "ShardingDbExecutor requires a routing context for non-typed execute.");
    }

    public Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default)
    {
        if (_currentRoutingContext.Value != null)
        {
            return ExecuteScalarWithContextAsync<T>(_currentRoutingContext.Value, sql, param, cancellationToken);
        }

        throw new InvalidOperationException(
            "ShardingDbExecutor requires a routing context for non-typed execute.");
    }

    public async Task<T> WithTransactionAsync<T>(Func<IDbTransaction, Task<T>> action, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default)
    {
        if (_currentRoutingContext.Value == null)
        {
            throw new InvalidOperationException("Routing context is required for transaction.");
        }

        var route = _router.Route(_currentRoutingContext.Value.EntityType, _currentRoutingContext.Value.ShardingKey);
        using var connection = _connectionManager.GetConnectionByRoute(route);

        if (connection is System.Data.Common.DbConnection dbConnection)
        {
            await dbConnection.OpenAsync(cancellationToken);
        }
        else
        {
            connection.Open();
        }

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

    public async Task WithTransactionAsync(Func<IDbTransaction, Task> action, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default)
    {
        await WithTransactionAsync(async tx =>
        {
            await action(tx);
            return 0;
        }, isolationLevel, cancellationToken);
    }

    public Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param, IDbTransaction transaction, CancellationToken cancellationToken = default)
    {
        var connection = transaction.Connection
            ?? throw new InvalidOperationException("Transaction has no associated connection");

        return connection.QueryAsync<T>(
            new CommandDefinition(sql, param, transaction, commandTimeout: _commandTimeout, cancellationToken: cancellationToken));
    }

    public Task<int> ExecuteAsync(string sql, object? param, IDbTransaction transaction, CancellationToken cancellationToken = default)
    {
        var connection = transaction.Connection
            ?? throw new InvalidOperationException("Transaction has no associated connection");

        return connection.ExecuteAsync(
            new CommandDefinition(sql, param, transaction, commandTimeout: _commandTimeout, cancellationToken: cancellationToken));
    }

    public Task<T?> ExecuteScalarAsync<T>(string sql, object? param, IDbTransaction transaction, CancellationToken cancellationToken = default)
    {
        var connection = transaction.Connection
            ?? throw new InvalidOperationException("Transaction has no associated connection");

        return connection.ExecuteScalarAsync<T>(
            new CommandDefinition(sql, param, transaction, commandTimeout: _commandTimeout, cancellationToken: cancellationToken));
    }

    #endregion

    #region IShardingDbExecutor Implementation

    public async Task<IEnumerable<T>> QueryAsync<T>(object shardingKey, string sql, object? param = null, CancellationToken cancellationToken = default) where T : class
    {
        var route = _router.Route<T>(shardingKey);
        return await QueryWithRouteAsync<T>(route, sql, param, cancellationToken);
    }

    public async Task<T?> QuerySingleOrDefaultAsync<T>(object shardingKey, string sql, object? param = null, CancellationToken cancellationToken = default) where T : class
    {
        var route = _router.Route<T>(shardingKey);
        return await QuerySingleOrDefaultWithRouteAsync<T>(route, sql, param, cancellationToken);
    }

    public async Task<int> ExecuteAsync<T>(T entity, string sql, object? param = null, CancellationToken cancellationToken = default) where T : class
    {
        var shardingKey = _metadataProvider.GetShardingKeyValue(entity);
        var route = _router.Route<T>(shardingKey, DataOperationType.Update);
        return await ExecuteWithRouteAsync(route, sql, param, cancellationToken);
    }

    public async Task<int> InsertAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class
    {
        var metadata = _metadataProvider.GetMetadata<T>();
        var shardingKey = _metadataProvider.GetShardingKeyValue(entity);
        var route = _router.Route<T>(shardingKey, DataOperationType.Insert);

        // 构建 INSERT SQL
        var (sql, parameters) = BuildInsertSql(entity, metadata, route.ActualTableName);

        return await ExecuteWithRouteAsync(route, sql, parameters, cancellationToken);
    }

    public async Task<int> InsertBatchAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class
    {
        var entityList = entities.ToList();
        if (entityList.Count == 0) return 0;

        var metadata = _metadataProvider.GetMetadata<T>();

        // 按分片分组
        var shardGroups = new Dictionary<int, List<T>>();
        foreach (var entity in entityList)
        {
            var shardingKey = _metadataProvider.GetShardingKeyValue(entity);
            var route = _router.Route<T>(shardingKey, DataOperationType.Insert);
            var shardIndex = route.ShardIndex ?? 0;

            if (!shardGroups.TryGetValue(shardIndex, out var list))
            {
                list = new List<T>();
                shardGroups[shardIndex] = list;
            }
            list.Add(entity);
        }

        // 并行执行各分片的批量插入
        var tasks = shardGroups.Select(async group =>
        {
            var route = _router.Route<T>(group.Key, DataOperationType.Insert);
            var (sql, parameters) = BuildBatchInsertSql(group.Value, metadata, route.ActualTableName);
            return await ExecuteWithRouteAsync(route, sql, parameters, cancellationToken);
        });

        var results = await Task.WhenAll(tasks);
        return results.Sum();
    }

    public Task<int> UpdateAsync<T>(T entity, Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default) where T : class
    {
        // 简化实现，实际应该构建完整的 UPDATE SQL
        throw new NotImplementedException("UpdateAsync with predicate is not yet implemented. Use ExecuteAsync with custom SQL instead.");
    }

    public async Task<int> DeleteAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class
    {
        var metadata = _metadataProvider.GetMetadata<T>();
        var shardingKey = _metadataProvider.GetShardingKeyValue(entity);
        var route = _router.Route<T>(shardingKey, DataOperationType.Delete);

        // 构建 DELETE SQL
        var shardingProp = _metadataProvider.GetShardingPropertyInfo(typeof(T));
        var keyValue = shardingProp?.ValueGetter?.Invoke(entity);

        var sql = $"DELETE FROM {route.ActualTableName} WHERE {metadata.ShardingKey} = @Id";
        var parameters = new { Id = keyValue };

        return await ExecuteWithRouteAsync(route, sql, parameters, cancellationToken);
    }

    public async Task<int> DeleteAsync<T>(Expression<Func<T, bool>> predicate, object shardingKey, CancellationToken cancellationToken = default) where T : class
    {
        var route = _router.Route<T>(shardingKey, DataOperationType.Delete);

        // 简化实现，实际应该解析表达式树构建 WHERE 子句
        throw new NotImplementedException("DeleteAsync with predicate is not yet implemented. Use ExecuteAsync with custom SQL instead.");
    }

    public async Task<IEnumerable<T>> QueryAllShardsAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default) where T : class
    {
        var metadata = _metadataProvider.GetMetadata<T>();

        if (!metadata.UseSharding)
        {
            var route = _router.Route<T>();
            return await QueryWithRouteAsync<T>(route, sql, param, cancellationToken);
        }

        // 获取所有分片的表名
        var tableNames = _router.GetAllShardTableNames<T>();

        // 并行查询所有分片
        var tasks = tableNames.Select(async tableName =>
        {
            var shardIndex = ExtractShardIndex(tableName, metadata.TableName);
            var route = _router.Route<T>(shardIndex);

            // 替换 SQL 中的表名
            var shardSql = ReplaceTableName(sql, metadata.TableName, route.ActualTableName);
            return await QueryWithRouteAsync<T>(route, shardSql, param, cancellationToken);
        });

        var results = await Task.WhenAll(tasks);
        return results.SelectMany(r => r);
    }

    public async Task<TResult> WithTransactionAsync<T, TResult>(T entity, Func<IDbTransaction, Task<TResult>> action, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default) where T : class
    {
        var shardingKey = _metadataProvider.GetShardingKeyValue(entity);
        var route = _router.Route<T>(shardingKey);

        using var connection = _connectionManager.GetConnectionByRoute(route);

        if (connection is System.Data.Common.DbConnection dbConnection)
        {
            await dbConnection.OpenAsync(cancellationToken);
        }
        else
        {
            connection.Open();
        }

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

    public RoutingContext? GetCurrentRoutingContext()
    {
        return _currentRoutingContext.Value;
    }

    public void SetRoutingContext(RoutingContext context)
    {
        _currentRoutingContext.Value = context;
    }

    public void ClearRoutingContext()
    {
        _currentRoutingContext.Value = null;
    }

    #endregion

    #region Private Helper Methods

    private async Task<IEnumerable<T>> QueryWithRouteAsync<T>(RoutingResult route, string sql, object? param, CancellationToken cancellationToken)
    {
        using var connection = await _connectionManager.GetOpenConnectionByRouteAsync(route, cancellationToken);
        return await connection.QueryAsync<T>(
            new CommandDefinition(sql, param, commandTimeout: _commandTimeout, cancellationToken: cancellationToken));
    }

    private async Task<T?> QuerySingleOrDefaultWithRouteAsync<T>(RoutingResult route, string sql, object? param, CancellationToken cancellationToken)
    {
        using var connection = await _connectionManager.GetOpenConnectionByRouteAsync(route, cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<T>(
            new CommandDefinition(sql, param, commandTimeout: _commandTimeout, cancellationToken: cancellationToken));
    }

    private async Task<int> ExecuteWithRouteAsync(RoutingResult route, string sql, object? param, CancellationToken cancellationToken)
    {
        using var connection = await _connectionManager.GetOpenConnectionByRouteAsync(route, cancellationToken);
        return await connection.ExecuteAsync(
            new CommandDefinition(sql, param, commandTimeout: _commandTimeout, cancellationToken: cancellationToken));
    }

    private async Task<T?> ExecuteScalarWithRouteAsync<T>(RoutingResult route, string sql, object? param, CancellationToken cancellationToken)
    {
        using var connection = await _connectionManager.GetOpenConnectionByRouteAsync(route, cancellationToken);
        return await connection.ExecuteScalarAsync<T>(
            new CommandDefinition(sql, param, commandTimeout: _commandTimeout, cancellationToken: cancellationToken));
    }

    private async Task<IEnumerable<T>> QueryWithContextAsync<T>(RoutingContext context, string sql, object? param, CancellationToken cancellationToken)
    {
        var route = _router.Route(context.EntityType, context.ShardingKey, context.OperationType);
        return await QueryWithRouteAsync<T>(route, sql, param, cancellationToken);
    }

    private async Task<T?> QuerySingleOrDefaultWithContextAsync<T>(RoutingContext context, string sql, object? param, CancellationToken cancellationToken)
    {
        var route = _router.Route(context.EntityType, context.ShardingKey, context.OperationType);
        return await QuerySingleOrDefaultWithRouteAsync<T>(route, sql, param, cancellationToken);
    }

    private async Task<T?> QueryFirstOrDefaultWithContextAsync<T>(RoutingContext context, string sql, object? param, CancellationToken cancellationToken)
    {
        var route = _router.Route(context.EntityType, context.ShardingKey, context.OperationType);
        using var connection = await _connectionManager.GetOpenConnectionByRouteAsync(route, cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<T>(
            new CommandDefinition(sql, param, commandTimeout: _commandTimeout, cancellationToken: cancellationToken));
    }

    private async Task<int> ExecuteWithContextAsync(RoutingContext context, string sql, object? param, CancellationToken cancellationToken)
    {
        var route = _router.Route(context.EntityType, context.ShardingKey, context.OperationType);
        return await ExecuteWithRouteAsync(route, sql, param, cancellationToken);
    }

    private async Task<T?> ExecuteScalarWithContextAsync<T>(RoutingContext context, string sql, object? param, CancellationToken cancellationToken)
    {
        var route = _router.Route(context.EntityType, context.ShardingKey, context.OperationType);
        return await ExecuteScalarWithRouteAsync<T>(route, sql, param, cancellationToken);
    }

    private (string Sql, object Parameters) BuildInsertSql<T>(T entity, EntityMetadata metadata, string actualTableName)
    {
        var properties = entity.GetType()
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(p => !metadata.IgnoredProperties.Contains(p.Name) && p.CanRead);

        var columns = new List<string>();
        var parameters = new Dictionary<string, object?>();

        foreach (var prop in properties)
        {
            var columnName = metadata.ColumnMappings.GetValueOrDefault(prop.Name, prop.Name);
            columns.Add(columnName);
            parameters[$"@{prop.Name}"] = prop.GetValue(entity);
        }

        var sql = $"INSERT INTO {actualTableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", parameters.Keys)})";

        return (sql, parameters);
    }

    private (string Sql, object Parameters) BuildBatchInsertSql<T>(List<T> entities, EntityMetadata metadata, string actualTableName) where T : class
    {
        // 简化实现，实际应该使用更高效的方式
        var sb = new StringBuilder();
        var allParameters = new Dictionary<string, object?>();

        var properties = typeof(T)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(p => !metadata.IgnoredProperties.Contains(p.Name) && p.CanRead)
            .ToList();

        var columnNames = properties.Select(p => metadata.ColumnMappings.GetValueOrDefault(p.Name, p.Name)).ToList();

        sb.AppendLine($"INSERT INTO {actualTableName} ({string.Join(", ", columnNames)}) VALUES");

        for (int i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];
            var valueParams = new List<string>();

            foreach (var prop in properties)
            {
                var paramName = $"@p{i}_{prop.Name}";
                valueParams.Add(paramName);
                allParameters[paramName] = prop.GetValue(entity);
            }

            sb.Append($"({string.Join(", ", valueParams)})");
            if (i < entities.Count - 1)
            {
                sb.AppendLine(",");
            }
        }

        return (sb.ToString(), allParameters);
    }

    private static int ExtractShardIndex(string tableName, string logicalTableName)
    {
        // 从表名中提取分片索引
        // 例如: orders_001 -> 1
        if (tableName.StartsWith(logicalTableName + "_"))
        {
            var suffix = tableName[(logicalTableName.Length + 1)..];
            if (int.TryParse(suffix, out var index))
            {
                return index;
            }
        }
        return 0;
    }

    private static string ReplaceTableName(string sql, string logicalTableName, string actualTableName)
    {
        // 简单的表名替换
        return sql.Replace(logicalTableName, actualTableName);
    }

    #endregion
}
