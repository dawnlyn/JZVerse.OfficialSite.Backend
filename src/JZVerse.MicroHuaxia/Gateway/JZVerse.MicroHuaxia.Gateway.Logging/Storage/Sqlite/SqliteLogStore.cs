using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using JZVerse.MicroHuaxia.Gateway.Logging.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Gateway.Logging.Storage.Sqlite;

/// <summary>
/// SQLite 日志存储实现
/// </summary>
public sealed class SqliteLogStore : ILogStore, IDisposable
{
    private readonly SqliteLogStoreOptions _options;
    private readonly ILogger<SqliteLogStore> _logger;
    private readonly SqliteSchemaManager _schemaManager;
    private readonly string _connectionString;
    private readonly Channel<LogEntry> _batchChannel;
    private readonly Timer _batchFlushTimer;
    private readonly Timer _maintenanceTimer;
    private readonly List<LogEntry> _batchBuffer = [];
    private readonly object _batchLock = new();
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    /// <summary>
    /// 创建 SQLite 日志存储
    /// </summary>
    public SqliteLogStore(
        IOptions<SqliteLogStoreOptions> options,
        ILogger<SqliteLogStore> logger,
        SqliteSchemaManager schemaManager)
    {
        _options = options.Value;
        _logger = logger;
        _schemaManager = schemaManager;

        // 确保目录存在
        var directory = Path.GetDirectoryName(_options.DatabasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // 构建连接字符串
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _options.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true
        };
        _connectionString = builder.ConnectionString;

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // 初始化数据库
        _ = InitializeDatabaseAsync();

        // 创建批量写入通道
        _batchChannel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        // 启动批量处理任务
        _ = ProcessBatchChannelAsync();

        // 定期刷新批量缓冲区
        _batchFlushTimer = new Timer(
            _ => _ = FlushBatchAsync(),
            null,
            TimeSpan.FromSeconds(_options.BatchFlushIntervalSeconds),
            TimeSpan.FromSeconds(_options.BatchFlushIntervalSeconds));

        // 定期维护（VACUUM、删除过期分区）
        _maintenanceTimer = new Timer(
            _ => _ = MaintenanceAsync(),
            null,
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(_options.VacuumIntervalDays));
    }

    private async Task InitializeDatabaseAsync()
    {
        await using var connection = await OpenConnectionAsync();

        if (_options.EnableWal)
        {
            await using var walCommand = new SqliteCommand("PRAGMA journal_mode=WAL;", connection);
            await walCommand.ExecuteNonQueryAsync();
        }

        // 确保默认表存在
        var tableName = _schemaManager.GetTableName(DateTimeOffset.UtcNow);
        await _schemaManager.EnsureTableExistsAsync(connection, tableName);
    }

    /// <inheritdoc />
    public Task AddAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        if (_disposed) return Task.CompletedTask;

        _batchChannel.Writer.TryWrite(entry);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task AddBatchAsync(IEnumerable<LogEntry> entries, CancellationToken cancellationToken = default)
    {
        if (_disposed) return Task.CompletedTask;

        foreach (var entry in entries)
        {
            _batchChannel.Writer.TryWrite(entry);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<LogQueryResult> QueryAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        await using var connection = await OpenConnectionAsync(cancellationToken);
        var tables = await _schemaManager.GetAllPartitionTablesAsync(connection, cancellationToken);

        if (tables.Count == 0)
        {
            return new LogQueryResult
            {
                Entries = [],
                TotalCount = 0,
                HasMore = false,
                QueryDurationMs = sw.Elapsed.TotalMilliseconds
            };
        }

        // 构建 UNION ALL 查询
        var (whereSql, parameters) = BuildWhereClause(query);
        var allEntries = new List<LogEntry>();

        foreach (var table in tables)
        {
            var sql = $"SELECT * FROM {table} {whereSql}";
            await using var command = new SqliteCommand(sql, connection);

            foreach (var param in parameters)
            {
                command.Parameters.AddWithValue(param.Key, param.Value);
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                allEntries.Add(ReadLogEntry(reader));
            }
        }

        // 排序
        allEntries = query.Descending
            ? allEntries.OrderByDescending(e => e.Timestamp).ToList()
            : allEntries.OrderBy(e => e.Timestamp).ToList();

        var totalCount = allEntries.Count;

        // 分页
        var paged = allEntries
            .Skip(query.Skip)
            .Take(Math.Min(query.Take, 1000))
            .ToList();

        sw.Stop();

        return new LogQueryResult
        {
            Entries = paged,
            TotalCount = totalCount,
            HasMore = query.Skip + paged.Count < totalCount,
            QueryDurationMs = sw.Elapsed.TotalMilliseconds,
            StartTime = query.StartTime,
            EndTime = query.EndTime
        };
    }

    /// <inheritdoc />
    public async Task<LogEntry?> GetByIdAsync(string logId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var tables = await _schemaManager.GetAllPartitionTablesAsync(connection, cancellationToken);

        foreach (var table in tables)
        {
            var sql = $"SELECT * FROM {table} WHERE Id = @Id LIMIT 1;";
            await using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@Id", logId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return ReadLogEntry(reader);
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LogEntry>> GetByTraceIdAsync(string traceId, CancellationToken cancellationToken = default)
    {
        var entries = new List<LogEntry>();

        await using var connection = await OpenConnectionAsync(cancellationToken);
        var tables = await _schemaManager.GetAllPartitionTablesAsync(connection, cancellationToken);

        foreach (var table in tables)
        {
            var sql = $"SELECT * FROM {table} WHERE TraceId = @TraceId ORDER BY Timestamp;";
            await using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@TraceId", traceId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                entries.Add(ReadLogEntry(reader));
            }
        }

        return entries.OrderBy(e => e.Timestamp).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LogEntry>> GetLatestAsync(int count, CancellationToken cancellationToken = default)
    {
        var entries = new List<LogEntry>();

        await using var connection = await OpenConnectionAsync(cancellationToken);
        var tables = await _schemaManager.GetAllPartitionTablesAsync(connection, cancellationToken);

        // 从最新的表开始查询
        foreach (var table in tables.OrderByDescending(t => t))
        {
            if (entries.Count >= count)
                break;

            var sql = $"SELECT * FROM {table} ORDER BY Timestamp DESC LIMIT @Limit;";
            await using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@Limit", count - entries.Count);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                entries.Add(ReadLogEntry(reader));
            }
        }

        return entries.OrderByDescending(e => e.Timestamp).Take(count).ToList();
    }

    /// <inheritdoc />
    public async Task<LogStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        long totalCount = 0;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        var tables = await _schemaManager.GetAllPartitionTablesAsync(connection, cancellationToken);

        foreach (var table in tables)
        {
            var sql = $"SELECT COUNT(*) FROM {table};";
            await using var command = new SqliteCommand(sql, connection);
            var count = await command.ExecuteScalarAsync(cancellationToken);
            totalCount += Convert.ToInt64(count);
        }

        // 获取数据库文件大小
        var fileSize = System.IO.File.Exists(_options.DatabasePath)
            ? new FileInfo(_options.DatabasePath).Length
            : 0;

        return new LogStatistics
        {
            TotalCount = totalCount,
            Storage = new StorageStatistics
            {
                StorageType = "SQLite",
                UsedBytes = fileSize,
                MaxBytes = 0, // SQLite 没有硬性限制
                PartitionCount = tables.Count
            }
        };
    }

    /// <inheritdoc />
    public async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await _schemaManager.DropExpiredPartitionsAsync(connection, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var tables = await _schemaManager.GetAllPartitionTablesAsync(connection, cancellationToken);

        foreach (var table in tables)
        {
            var sql = $"DROP TABLE IF EXISTS {table};";
            await using var command = new SqliteCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);

            var ftsSql = $"DROP TABLE IF EXISTS {table}_fts;";
            await using var ftsCommand = new SqliteCommand(ftsSql, connection);
            await ftsCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        _logger.LogInformation("已清空所有 SQLite 日志数据");
    }

    /// <inheritdoc />
    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = new SqliteCommand("SELECT 1;", connection);
            await command.ExecuteScalarAsync(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private async Task ProcessBatchChannelAsync()
    {
        await foreach (var entry in _batchChannel.Reader.ReadAllAsync())
        {
            if (_disposed) break;

            lock (_batchLock)
            {
                _batchBuffer.Add(entry);
            }

            if (_batchBuffer.Count >= _options.BatchInsertSize)
            {
                await FlushBatchAsync();
            }
        }
    }

    private async Task FlushBatchAsync()
    {
        List<LogEntry> entries;
        lock (_batchLock)
        {
            if (_batchBuffer.Count == 0)
                return;

            entries = [.. _batchBuffer];
            _batchBuffer.Clear();
        }

        try
        {
            await using var connection = await OpenConnectionAsync();
            await using var transaction = connection.BeginTransaction();

            // 按分区分组
            var groups = entries.GroupBy(e => _schemaManager.GetTableName(e.Timestamp));

            foreach (var group in groups)
            {
                var tableName = group.Key;
                await _schemaManager.EnsureTableExistsAsync(connection, tableName);

                foreach (var entry in group)
                {
                    await InsertEntryAsync(connection, tableName, entry);
                }
            }

            transaction.Commit();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量写入 SQLite 失败，共 {Count} 条日志", entries.Count);
        }
    }

    private async Task InsertEntryAsync(SqliteConnection connection, string tableName, LogEntry entry)
    {
        var sql = $@"
            INSERT INTO {tableName} (
                Id, Timestamp, Level, Category, EventId, EventName, Message, Exception,
                TraceId, SpanId, ParentSpanId, ServiceName, ServiceInstanceId, HostName, Environment,
                RequestPath, RequestMethod, RequestId, ClientIp, UserId, StatusCode, DurationMs,
                Properties, Scopes, Source, Tags
            ) VALUES (
                @Id, @Timestamp, @Level, @Category, @EventId, @EventName, @Message, @Exception,
                @TraceId, @SpanId, @ParentSpanId, @ServiceName, @ServiceInstanceId, @HostName, @Environment,
                @RequestPath, @RequestMethod, @RequestId, @ClientIp, @UserId, @StatusCode, @DurationMs,
                @Properties, @Scopes, @Source, @Tags
            );";

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", entry.Id);
        command.Parameters.AddWithValue("@Timestamp", entry.Timestamp.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("@Level", (int)entry.Level);
        command.Parameters.AddWithValue("@Category", entry.Category ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@EventId", entry.EventId);
        command.Parameters.AddWithValue("@EventName", entry.EventName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Message", entry.Message ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Exception", entry.Exception ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@TraceId", entry.TraceId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@SpanId", entry.SpanId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ParentSpanId", entry.ParentSpanId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ServiceName", entry.ServiceName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ServiceInstanceId", entry.ServiceInstanceId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@HostName", entry.HostName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Environment", entry.Environment ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@RequestPath", entry.RequestPath ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@RequestMethod", entry.RequestMethod ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@RequestId", entry.RequestId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ClientIp", entry.ClientIp ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@UserId", entry.UserId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@StatusCode", entry.StatusCode ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@DurationMs", entry.DurationMs ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Properties", entry.Properties is not null ? JsonSerializer.Serialize(entry.Properties, _jsonOptions) : DBNull.Value);
        command.Parameters.AddWithValue("@Scopes", entry.Scopes is not null ? JsonSerializer.Serialize(entry.Scopes, _jsonOptions) : DBNull.Value);
        command.Parameters.AddWithValue("@Source", (int)entry.Source);
        command.Parameters.AddWithValue("@Tags", entry.Tags is not null ? JsonSerializer.Serialize(entry.Tags, _jsonOptions) : DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    private LogEntry ReadLogEntry(SqliteDataReader reader)
    {
        return new LogEntry
        {
            Id = reader.GetString(reader.GetOrdinal("Id")),
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(reader.GetOrdinal("Timestamp"))),
            Level = (Microsoft.Extensions.Logging.LogLevel)reader.GetInt32(reader.GetOrdinal("Level")),
            Category = GetNullableString(reader, "Category") ?? string.Empty,
            EventId = reader.GetInt32(reader.GetOrdinal("EventId")),
            EventName = GetNullableString(reader, "EventName"),
            Message = GetNullableString(reader, "Message") ?? string.Empty,
            Exception = GetNullableString(reader, "Exception"),
            TraceId = GetNullableString(reader, "TraceId"),
            SpanId = GetNullableString(reader, "SpanId"),
            ParentSpanId = GetNullableString(reader, "ParentSpanId"),
            ServiceName = GetNullableString(reader, "ServiceName"),
            ServiceInstanceId = GetNullableString(reader, "ServiceInstanceId"),
            HostName = GetNullableString(reader, "HostName"),
            Environment = GetNullableString(reader, "Environment"),
            RequestPath = GetNullableString(reader, "RequestPath"),
            RequestMethod = GetNullableString(reader, "RequestMethod"),
            RequestId = GetNullableString(reader, "RequestId"),
            ClientIp = GetNullableString(reader, "ClientIp"),
            UserId = GetNullableString(reader, "UserId"),
            StatusCode = GetNullableInt(reader, "StatusCode"),
            DurationMs = GetNullableDouble(reader, "DurationMs"),
            Properties = DeserializeJson<Dictionary<string, object?>>(GetNullableString(reader, "Properties")),
            Scopes = DeserializeJson<List<Dictionary<string, object?>>>(GetNullableString(reader, "Scopes"))
                ?.Select(d => (IReadOnlyDictionary<string, object?>)d).ToList(),
            Source = (LogSource)reader.GetInt32(reader.GetOrdinal("Source")),
            Tags = DeserializeJson<List<string>>(GetNullableString(reader, "Tags"))
        };
    }

    private static string? GetNullableString(SqliteDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static int? GetNullableInt(SqliteDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static double? GetNullableDouble(SqliteDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDouble(ordinal);
    }

    private T? DeserializeJson<T>(string? json) where T : class
    {
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static (string sql, Dictionary<string, object> parameters) BuildWhereClause(LogQuery query)
    {
        var conditions = new List<string>();
        var parameters = new Dictionary<string, object>();

        if (query.StartTime.HasValue)
        {
            conditions.Add("Timestamp >= @StartTime");
            parameters["@StartTime"] = query.StartTime.Value.ToUnixTimeMilliseconds();
        }

        if (query.EndTime.HasValue)
        {
            conditions.Add("Timestamp <= @EndTime");
            parameters["@EndTime"] = query.EndTime.Value.ToUnixTimeMilliseconds();
        }

        if (query.MinLevel.HasValue)
        {
            conditions.Add("Level >= @MinLevel");
            parameters["@MinLevel"] = (int)query.MinLevel.Value;
        }

        if (!string.IsNullOrEmpty(query.ServiceName))
        {
            conditions.Add("ServiceName = @ServiceName");
            parameters["@ServiceName"] = query.ServiceName;
        }

        if (!string.IsNullOrEmpty(query.TraceId))
        {
            conditions.Add("TraceId = @TraceId");
            parameters["@TraceId"] = query.TraceId;
        }

        if (!string.IsNullOrEmpty(query.SearchText))
        {
            conditions.Add("(Message LIKE @SearchText OR Exception LIKE @SearchText)");
            parameters["@SearchText"] = $"%{query.SearchText}%";
        }

        var whereClause = conditions.Count > 0
            ? "WHERE " + string.Join(" AND ", conditions)
            : "";

        return (whereClause, parameters);
    }

    private async Task MaintenanceAsync()
    {
        try
        {
            await using var connection = await OpenConnectionAsync();

            // 删除过期分区
            await _schemaManager.DropExpiredPartitionsAsync(connection);

            // VACUUM
            await using var vacuumCommand = new SqliteCommand("VACUUM;", connection);
            await vacuumCommand.ExecuteNonQueryAsync();

            _logger.LogInformation("SQLite 维护任务完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SQLite 维护任务失败");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _batchChannel.Writer.Complete();
        _batchFlushTimer.Dispose();
        _maintenanceTimer.Dispose();

        // 刷新剩余的批量数据
        _ = FlushBatchAsync();
    }
}
