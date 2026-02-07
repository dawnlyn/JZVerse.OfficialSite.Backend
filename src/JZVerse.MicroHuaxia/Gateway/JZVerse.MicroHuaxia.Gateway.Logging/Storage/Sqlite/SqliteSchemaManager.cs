using JZVerse.MicroHuaxia.Gateway.Logging.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Gateway.Logging.Storage.Sqlite;

/// <summary>
/// SQLite 表结构管理器
/// </summary>
public sealed class SqliteSchemaManager
{
    private readonly SqliteLogStoreOptions _options;
    private readonly ILogger<SqliteSchemaManager> _logger;
    private readonly HashSet<string> _existingTables = [];
    private readonly object _tableLock = new();

    /// <summary>
    /// 创建表结构管理器
    /// </summary>
    public SqliteSchemaManager(
        IOptions<SqliteLogStoreOptions> options,
        ILogger<SqliteSchemaManager> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 获取分区表名称
    /// </summary>
    public string GetTableName(DateTimeOffset timestamp)
    {
        if (!_options.EnablePartitioning)
            return "logs";

        return _options.PartitionStrategy switch
        {
            PartitionStrategy.Daily => $"logs_{timestamp.UtcDateTime:yyyyMMdd}",
            PartitionStrategy.Monthly => $"logs_{timestamp.UtcDateTime:yyyyMM}",
            _ => "logs"
        };
    }

    /// <summary>
    /// 确保表存在
    /// </summary>
    public async Task EnsureTableExistsAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken = default)
    {
        lock (_tableLock)
        {
            if (_existingTables.Contains(tableName))
                return;
        }

        await CreateTableAsync(connection, tableName, cancellationToken);

        lock (_tableLock)
        {
            _existingTables.Add(tableName);
        }
    }

    /// <summary>
    /// 创建表
    /// </summary>
    private async Task CreateTableAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        var sql = $@"
            CREATE TABLE IF NOT EXISTS {tableName} (
                Id TEXT PRIMARY KEY,
                Timestamp INTEGER NOT NULL,
                Level INTEGER NOT NULL,
                Category TEXT,
                EventId INTEGER,
                EventName TEXT,
                Message TEXT,
                Exception TEXT,
                TraceId TEXT,
                SpanId TEXT,
                ParentSpanId TEXT,
                ServiceName TEXT,
                ServiceInstanceId TEXT,
                HostName TEXT,
                Environment TEXT,
                RequestPath TEXT,
                RequestMethod TEXT,
                RequestId TEXT,
                ClientIp TEXT,
                UserId TEXT,
                StatusCode INTEGER,
                DurationMs REAL,
                Properties TEXT,
                Scopes TEXT,
                Source INTEGER,
                Tags TEXT
            );";

        await using var command = new SqliteCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);

        // 创建索引
        if (_options.CreateIndexes)
        {
            await CreateIndexesAsync(connection, tableName, cancellationToken);
        }

        // 创建全文搜索表
        if (_options.EnableFullTextSearch)
        {
            await CreateFtsTableAsync(connection, tableName, cancellationToken);
        }

        _logger.LogDebug("已创建日志表: {TableName}", tableName);
    }

    /// <summary>
    /// 创建索引
    /// </summary>
    private async Task CreateIndexesAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        var indexes = new List<string>
        {
            $"CREATE INDEX IF NOT EXISTS idx_{tableName}_timestamp ON {tableName}(Timestamp);",
            $"CREATE INDEX IF NOT EXISTS idx_{tableName}_level ON {tableName}(Level);",
            $"CREATE INDEX IF NOT EXISTS idx_{tableName}_traceid ON {tableName}(TraceId);",
            $"CREATE INDEX IF NOT EXISTS idx_{tableName}_servicename ON {tableName}(ServiceName);",
            $"CREATE INDEX IF NOT EXISTS idx_{tableName}_requestpath ON {tableName}(RequestPath);",
            $"CREATE INDEX IF NOT EXISTS idx_{tableName}_service_level ON {tableName}(ServiceName, Level);"
        };

        foreach (var sql in indexes)
        {
            await using var command = new SqliteCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    /// <summary>
    /// 创建全文搜索表
    /// </summary>
    private async Task CreateFtsTableAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        var ftsTableName = $"{tableName}_fts";
        var sql = $@"
            CREATE VIRTUAL TABLE IF NOT EXISTS {ftsTableName} USING fts5(
                Id,
                Message,
                Exception,
                content={tableName},
                content_rowid=rowid
            );";

        try
        {
            await using var command = new SqliteCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "创建全文搜索表失败: {TableName}", ftsTableName);
        }
    }

    /// <summary>
    /// 获取所有分区表名称
    /// </summary>
    public async Task<IReadOnlyList<string>> GetAllPartitionTablesAsync(SqliteConnection connection, CancellationToken cancellationToken = default)
    {
        var tables = new List<string>();

        var sql = "SELECT name FROM sqlite_master WHERE type='table' AND name LIKE 'logs%' AND name NOT LIKE '%_fts%';";
        await using var command = new SqliteCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    /// <summary>
    /// 删除过期分区
    /// </summary>
    public async Task<int> DropExpiredPartitionsAsync(SqliteConnection connection, CancellationToken cancellationToken = default)
    {
        if (!_options.EnablePartitioning)
            return 0;

        var cutoff = DateTimeOffset.UtcNow.AddDays(-_options.PartitionRetentionDays);
        var cutoffTableName = GetTableName(cutoff);

        var tables = await GetAllPartitionTablesAsync(connection, cancellationToken);
        var droppedCount = 0;

        foreach (var table in tables)
        {
            // 比较表名确定是否过期
            if (string.Compare(table, cutoffTableName, StringComparison.Ordinal) < 0)
            {
                try
                {
                    var dropSql = $"DROP TABLE IF EXISTS {table};";
                    await using var command = new SqliteCommand(dropSql, connection);
                    await command.ExecuteNonQueryAsync(cancellationToken);

                    // 同时删除 FTS 表
                    var dropFtsSql = $"DROP TABLE IF EXISTS {table}_fts;";
                    await using var ftsCommand = new SqliteCommand(dropFtsSql, connection);
                    await ftsCommand.ExecuteNonQueryAsync(cancellationToken);

                    droppedCount++;
                    _logger.LogInformation("已删除过期分区: {TableName}", table);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "删除分区失败: {TableName}", table);
                }
            }
        }

        return droppedCount;
    }
}
