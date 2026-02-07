using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using JZVerse.MicroHuaxia.Gateway.Logging.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Gateway.Logging.Storage.File;

/// <summary>
/// 文件日志存储实现
/// </summary>
public sealed class FileLogStore : ILogStore, IDisposable
{
    private readonly FileLogStoreOptions _options;
    private readonly ILogger<FileLogStore> _logger;
    private readonly LogFileRotator _rotator;
    private readonly LogFileCompressor _compressor;
    private readonly LogFileRetentionPolicy _retentionPolicy;
    private readonly Channel<LogEntry> _writeChannel;
    private readonly ConcurrentDictionary<string, StreamWriter> _writers = new();
    private readonly Timer _flushTimer;
    private readonly Timer _maintenanceTimer;
    private readonly object _writerLock = new();
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    /// <summary>
    /// 创建文件日志存储
    /// </summary>
    public FileLogStore(
        IOptions<FileLogStoreOptions> options,
        ILogger<FileLogStore> logger,
        LogFileRotator rotator,
        LogFileCompressor compressor,
        LogFileRetentionPolicy retentionPolicy)
    {
        _options = options.Value;
        _logger = logger;
        _rotator = rotator;
        _compressor = compressor;
        _retentionPolicy = retentionPolicy;

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // 创建写入通道
        _writeChannel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        // 启动后台写入任务
        _ = ProcessWriteChannelAsync();

        // 定期刷新
        _flushTimer = new Timer(
            _ => FlushAll(),
            null,
            TimeSpan.FromSeconds(_options.FlushIntervalSeconds),
            TimeSpan.FromSeconds(_options.FlushIntervalSeconds));

        // 定期维护（压缩、清理）
        _maintenanceTimer = new Timer(
            _ => _ = MaintenanceAsync(),
            null,
            TimeSpan.FromHours(_options.RetentionCheckIntervalHours),
            TimeSpan.FromHours(_options.RetentionCheckIntervalHours));

        // 确保基础目录存在
        Directory.CreateDirectory(_options.BasePath);
    }

    /// <inheritdoc />
    public Task AddAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        if (_disposed) return Task.CompletedTask;

        _writeChannel.Writer.TryWrite(entry);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task AddBatchAsync(IEnumerable<LogEntry> entries, CancellationToken cancellationToken = default)
    {
        if (_disposed) return Task.CompletedTask;

        foreach (var entry in entries)
        {
            _writeChannel.Writer.TryWrite(entry);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<LogQueryResult> QueryAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var entries = new List<LogEntry>();

        // 确定需要搜索的文件
        var files = GetLogFilesForQuery(query);

        foreach (var file in files)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var fileEntries = await ReadLogFileAsync(file, cancellationToken);
            entries.AddRange(fileEntries.Where(e => MatchesQuery(e, query)));
        }

        // 排序
        entries = query.Descending
            ? entries.OrderByDescending(e => e.Timestamp).ToList()
            : entries.OrderBy(e => e.Timestamp).ToList();

        var totalCount = entries.Count;

        // 分页
        var paged = entries
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
        var files = GetAllLogFiles();

        foreach (var file in files)
        {
            var entries = await ReadLogFileAsync(file, cancellationToken);
            var found = entries.FirstOrDefault(e => e.Id == logId);
            if (found is not null)
                return found;
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LogEntry>> GetByTraceIdAsync(string traceId, CancellationToken cancellationToken = default)
    {
        var result = new List<LogEntry>();
        var files = GetAllLogFiles();

        foreach (var file in files)
        {
            var entries = await ReadLogFileAsync(file, cancellationToken);
            result.AddRange(entries.Where(e => e.TraceId == traceId));
        }

        return result.OrderBy(e => e.Timestamp).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LogEntry>> GetLatestAsync(int count, CancellationToken cancellationToken = default)
    {
        var result = new List<LogEntry>();
        var files = GetAllLogFiles().OrderByDescending(f => new FileInfo(f).LastWriteTimeUtc);

        foreach (var file in files)
        {
            if (result.Count >= count)
                break;

            var entries = await ReadLogFileAsync(file, cancellationToken);
            result.AddRange(entries);
        }

        return result
            .OrderByDescending(e => e.Timestamp)
            .Take(count)
            .ToList();
    }

    /// <inheritdoc />
    public Task<LogStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var (totalSize, fileCount, compressedCount) = _retentionPolicy.GetStorageStats();

        var stats = new LogStatistics
        {
            TotalCount = 0, // 文件存储不维护精确计数
            Storage = new StorageStatistics
            {
                StorageType = "File",
                UsedBytes = totalSize,
                MaxBytes = _options.MaxTotalSizeBytes,
                FileCount = fileCount,
                CompressedFileCount = compressedCount
            }
        };

        return Task.FromResult(stats);
    }

    /// <inheritdoc />
    public async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        await _retentionPolicy.ApplyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        // 关闭所有写入器
        CloseAllWriters();

        // 删除所有文件
        if (Directory.Exists(_options.BasePath))
        {
            foreach (var file in Directory.EnumerateFiles(_options.BasePath, "*", SearchOption.AllDirectories))
            {
                try { System.IO.File.Delete(file); } catch { }
            }

            // 删除空目录
            foreach (var dir in Directory.EnumerateDirectories(_options.BasePath, "*", SearchOption.AllDirectories)
                .OrderByDescending(d => d.Length))
            {
                try { Directory.Delete(dir); } catch { }
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(_options.BasePath);
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private async Task ProcessWriteChannelAsync()
    {
        await foreach (var entry in _writeChannel.Reader.ReadAllAsync())
        {
            if (_disposed) break;

            try
            {
                await WriteEntryAsync(entry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "写入日志文件失败");
            }
        }
    }

    private async Task WriteEntryAsync(LogEntry entry)
    {
        var filePath = GetFilePath(entry);
        var writer = GetOrCreateWriter(filePath);

        // 检查是否需要轮转
        if (_rotator.NeedsRotation(filePath))
        {
            CloseWriter(filePath);
            _rotator.Rotate(filePath);
            writer = GetOrCreateWriter(filePath);
        }

        var json = JsonSerializer.Serialize(entry, _jsonOptions);
        await writer.WriteLineAsync(json);
    }

    private string GetFilePath(LogEntry entry)
    {
        var pattern = _options.FileNamePattern;
        var timestamp = _options.UseUtcTime ? entry.Timestamp.UtcDateTime : entry.Timestamp.LocalDateTime;

        var fileName = pattern
            .Replace("{Service}", SanitizeFileName(entry.ServiceName ?? "Unknown"))
            .Replace("{Level}", entry.Level.ToString())
            .Replace("{Date}", timestamp.ToString("yyyy-MM-dd"))
            .Replace("{Hour}", timestamp.ToString("HH"));

        return Path.Combine(_options.BasePath, fileName);
    }

    private static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Concat(name.Where(c => !invalidChars.Contains(c)));
    }

    private StreamWriter GetOrCreateWriter(string filePath)
    {
        return _writers.GetOrAdd(filePath, path =>
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return new StreamWriter(
                new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, _options.WriteBufferSize),
                System.Text.Encoding.UTF8,
                _options.WriteBufferSize)
            {
                AutoFlush = false
            };
        });
    }

    private void CloseWriter(string filePath)
    {
        if (_writers.TryRemove(filePath, out var writer))
        {
            try
            {
                writer.Flush();
                writer.Dispose();
            }
            catch { }
        }
    }

    private void CloseAllWriters()
    {
        foreach (var path in _writers.Keys.ToList())
        {
            CloseWriter(path);
        }
    }

    private void FlushAll()
    {
        foreach (var writer in _writers.Values)
        {
            try { writer.Flush(); } catch { }
        }
    }

    private async Task MaintenanceAsync()
    {
        try
        {
            await _compressor.CompressEligibleFilesAsync();
            await _retentionPolicy.ApplyAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "日志文件维护任务失败");
        }
    }

    private IEnumerable<string> GetAllLogFiles()
    {
        if (!Directory.Exists(_options.BasePath))
            return [];

        return Directory.EnumerateFiles(_options.BasePath, "*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".log") || f.EndsWith(".log.gz"));
    }

    private IEnumerable<string> GetLogFilesForQuery(LogQuery query)
    {
        // TODO: 根据查询条件优化文件选择
        return GetAllLogFiles();
    }

    private async Task<IReadOnlyList<LogEntry>> ReadLogFileAsync(string filePath, CancellationToken cancellationToken)
    {
        var entries = new List<LogEntry>();

        Stream? stream = null;
        try
        {
            if (filePath.EndsWith(".gz"))
            {
                var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                stream = new System.IO.Compression.GZipStream(fileStream, System.IO.Compression.CompressionMode.Decompress);
            }
            else
            {
                stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            }

            using var reader = new StreamReader(stream);
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    var entry = JsonSerializer.Deserialize<LogEntry>(line, _jsonOptions);
                    if (entry is not null)
                    {
                        entries.Add(entry);
                    }
                }
                catch
                {
                    // 跳过无法解析的行
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取日志文件失败: {FilePath}", filePath);
        }
        finally
        {
            stream?.Dispose();
        }

        return entries;
    }

    private static bool MatchesQuery(LogEntry entry, LogQuery query)
    {
        if (query.StartTime.HasValue && entry.Timestamp < query.StartTime.Value)
            return false;

        if (query.EndTime.HasValue && entry.Timestamp > query.EndTime.Value)
            return false;

        if (query.MinLevel.HasValue && entry.Level < query.MinLevel.Value)
            return false;

        if (!string.IsNullOrEmpty(query.ServiceName) && entry.ServiceName != query.ServiceName)
            return false;

        if (!string.IsNullOrEmpty(query.TraceId) && entry.TraceId != query.TraceId)
            return false;

        if (!string.IsNullOrEmpty(query.SearchText))
        {
            var searchText = query.CaseSensitive ? query.SearchText : query.SearchText.ToLowerInvariant();
            var message = query.CaseSensitive ? entry.Message : entry.Message.ToLowerInvariant();

            if (!message.Contains(searchText))
            {
                if (string.IsNullOrEmpty(entry.Exception))
                    return false;

                var exception = query.CaseSensitive ? entry.Exception : entry.Exception.ToLowerInvariant();
                if (!exception.Contains(searchText))
                    return false;
            }
        }

        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _writeChannel.Writer.Complete();
        _flushTimer.Dispose();
        _maintenanceTimer.Dispose();
        CloseAllWriters();
    }
}
