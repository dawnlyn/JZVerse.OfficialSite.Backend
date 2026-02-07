using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using JZVerse.MicroHuaxia.Gateway.Logging.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Gateway.Logging.Storage.InMemory;

/// <summary>
/// 内存日志存储实现
/// </summary>
public sealed class InMemoryLogStore : IStreamableLogStore, IDisposable
{
    private readonly InMemoryLogStoreOptions _options;
    private readonly ILogger<InMemoryLogStore> _logger;
    private readonly CircularBuffer<LogEntry> _buffer;
    private readonly ConcurrentDictionary<string, List<string>> _traceIdIndex = new();
    private readonly ConcurrentDictionary<string, List<string>> _requestIdIndex = new();
    private readonly ConcurrentDictionary<string, List<string>> _serviceNameIndex = new();
    private readonly ConcurrentDictionary<string, LogEntry> _idIndex = new();
    private readonly Timer? _cleanupTimer;
    private readonly object _indexLock = new();
    private readonly Channel<LogEntry> _streamChannel;
    private bool _disposed;

    /// <inheritdoc />
    public event EventHandler<LogEntry>? LogAdded;

    /// <summary>
    /// 创建内存日志存储
    /// </summary>
    public InMemoryLogStore(
        IOptions<InMemoryLogStoreOptions> options,
        ILogger<InMemoryLogStore> logger)
    {
        _options = options.Value;
        _logger = logger;
        _buffer = new CircularBuffer<LogEntry>(_options.MaxCapacity);
        _streamChannel = Channel.CreateUnbounded<LogEntry>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });

        // 启动清理定时器
        if (_options.RetentionMinutes > 0)
        {
            var cleanupInterval = TimeSpan.FromMinutes(_options.CleanupIntervalMinutes);
            _cleanupTimer = new Timer(
                _ => _ = CleanupAsync(),
                null,
                cleanupInterval,
                cleanupInterval);
        }
    }

    /// <inheritdoc />
    public Task AddAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        if (_disposed) return Task.CompletedTask;

        // 添加到循环缓冲区
        var overwritten = _buffer.Add(entry);

        // 更新索引
        if (_options.EnableIndexing)
        {
            UpdateIndexes(entry, overwritten);
        }

        // 触发事件
        LogAdded?.Invoke(this, entry);

        // 写入流通道
        _streamChannel.Writer.TryWrite(entry);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task AddBatchAsync(IEnumerable<LogEntry> entries, CancellationToken cancellationToken = default)
    {
        if (_disposed) return Task.CompletedTask;

        foreach (var entry in entries)
        {
            var overwritten = _buffer.Add(entry);

            if (_options.EnableIndexing)
            {
                UpdateIndexes(entry, overwritten);
            }

            LogAdded?.Invoke(this, entry);
            _streamChannel.Writer.TryWrite(entry);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<LogQueryResult> QueryAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        // 获取所有日志
        var entries = _buffer.ToList();

        // 应用过滤条件
        var filtered = ApplyFilters(entries, query);

        // 排序
        filtered = ApplySorting(filtered, query);

        // 计算总数
        var totalCount = filtered.Count;

        // 分页
        var paged = filtered
            .Skip(query.Skip)
            .Take(Math.Min(query.Take, 1000))
            .ToList();

        sw.Stop();

        var result = new LogQueryResult
        {
            Entries = paged,
            TotalCount = totalCount,
            HasMore = query.Skip + paged.Count < totalCount,
            QueryDurationMs = sw.Elapsed.TotalMilliseconds,
            StartTime = query.StartTime,
            EndTime = query.EndTime
        };

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<LogEntry?> GetByIdAsync(string logId, CancellationToken cancellationToken = default)
    {
        if (_options.EnableIndexing && _idIndex.TryGetValue(logId, out var entry))
        {
            return Task.FromResult<LogEntry?>(entry);
        }

        // 回退到遍历查找
        var found = _buffer.Where(e => e.Id == logId).FirstOrDefault();
        return Task.FromResult(found);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<LogEntry>> GetByTraceIdAsync(string traceId, CancellationToken cancellationToken = default)
    {
        if (_options.EnableIndexing && _traceIdIndex.TryGetValue(traceId, out var logIds))
        {
            var entries = logIds
                .Select(id => _idIndex.TryGetValue(id, out var e) ? e : null)
                .Where(e => e is not null)
                .OrderBy(e => e!.Timestamp)
                .ToList();

            return Task.FromResult<IReadOnlyList<LogEntry>>(entries!);
        }

        // 回退到遍历查找
        var found = _buffer
            .Where(e => e.TraceId == traceId)
            .OrderBy(e => e.Timestamp)
            .ToList();

        return Task.FromResult<IReadOnlyList<LogEntry>>(found);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<LogEntry>> GetLatestAsync(int count, CancellationToken cancellationToken = default)
    {
        var entries = _buffer.GetLatest(count);
        return Task.FromResult(entries);
    }

    /// <inheritdoc />
    public Task<LogStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var entries = _buffer.ToList();

        var countByLevel = entries
            .GroupBy(e => e.Level)
            .ToDictionary(g => g.Key, g => (long)g.Count());

        var countByService = entries
            .Where(e => !string.IsNullOrEmpty(e.ServiceName))
            .GroupBy(e => e.ServiceName!)
            .ToDictionary(g => g.Key, g => (long)g.Count());

        var countBySource = entries
            .GroupBy(e => e.Source)
            .ToDictionary(g => g.Key, g => (long)g.Count());

        var errorCount = entries.Count(e => e.Level is LogLevel.Error or LogLevel.Critical);
        var errorRate = entries.Count > 0 ? (double)errorCount / entries.Count : 0;

        var earliestTimestamp = entries.Count > 0 ? entries.Min(e => e.Timestamp) : (DateTimeOffset?)null;
        var latestTimestamp = entries.Count > 0 ? entries.Max(e => e.Timestamp) : (DateTimeOffset?)null;

        // 计算每分钟日志数
        double logsPerMinute = 0;
        if (earliestTimestamp.HasValue && latestTimestamp.HasValue)
        {
            var duration = latestTimestamp.Value - earliestTimestamp.Value;
            if (duration.TotalMinutes > 0)
            {
                logsPerMinute = entries.Count / duration.TotalMinutes;
            }
        }

        var stats = new LogStatistics
        {
            TotalCount = entries.Count,
            CountByLevel = countByLevel,
            CountByService = countByService,
            CountBySource = countBySource,
            EarliestTimestamp = earliestTimestamp,
            LatestTimestamp = latestTimestamp,
            ErrorRate = errorRate,
            LogsPerMinute = logsPerMinute,
            Storage = new StorageStatistics
            {
                StorageType = "InMemory",
                UsedBytes = entries.Count * 1024, // 估算每条日志约 1KB
                MaxBytes = _options.MaxCapacity * 1024
            }
        };

        return Task.FromResult(stats);
    }

    /// <inheritdoc />
    public Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        if (_options.RetentionMinutes <= 0) return Task.CompletedTask;

        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-_options.RetentionMinutes);
        var removed = _buffer.RemoveWhere(e => e.Timestamp < cutoff);

        if (removed > 0)
        {
            _logger.LogDebug("已清理 {Count} 条过期日志", removed);

            // 重建索引
            if (_options.EnableIndexing)
            {
                RebuildIndexes();
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _buffer.Clear();

        if (_options.EnableIndexing)
        {
            lock (_indexLock)
            {
                _idIndex.Clear();
                _traceIdIndex.Clear();
                _requestIdIndex.Clear();
                _serviceNameIndex.Clear();
            }
        }

        _logger.LogInformation("已清空所有内存日志");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(!_disposed);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<LogEntry> StreamAsync(
        LogQuery? filter = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var entry in _streamChannel.Reader.ReadAllAsync(cancellationToken))
        {
            if (filter is null || MatchesFilter(entry, filter))
            {
                yield return entry;
            }
        }
    }

    private void UpdateIndexes(LogEntry entry, LogEntry? overwritten)
    {
        lock (_indexLock)
        {
            // 移除被覆盖的旧条目的索引
            if (overwritten is not null)
            {
                RemoveFromIndexes(overwritten);
            }

            // 添加新条目的索引
            _idIndex[entry.Id] = entry;

            if (!string.IsNullOrEmpty(entry.TraceId))
            {
                var list = _traceIdIndex.GetOrAdd(entry.TraceId, _ => []);
                list.Add(entry.Id);
            }

            if (!string.IsNullOrEmpty(entry.RequestId))
            {
                var list = _requestIdIndex.GetOrAdd(entry.RequestId, _ => []);
                list.Add(entry.Id);
            }

            if (!string.IsNullOrEmpty(entry.ServiceName))
            {
                var list = _serviceNameIndex.GetOrAdd(entry.ServiceName, _ => []);
                list.Add(entry.Id);
            }
        }
    }

    private void RemoveFromIndexes(LogEntry entry)
    {
        _idIndex.TryRemove(entry.Id, out _);

        if (!string.IsNullOrEmpty(entry.TraceId) && _traceIdIndex.TryGetValue(entry.TraceId, out var traceList))
        {
            traceList.Remove(entry.Id);
            if (traceList.Count == 0)
            {
                _traceIdIndex.TryRemove(entry.TraceId, out _);
            }
        }

        if (!string.IsNullOrEmpty(entry.RequestId) && _requestIdIndex.TryGetValue(entry.RequestId, out var requestList))
        {
            requestList.Remove(entry.Id);
            if (requestList.Count == 0)
            {
                _requestIdIndex.TryRemove(entry.RequestId, out _);
            }
        }

        if (!string.IsNullOrEmpty(entry.ServiceName) && _serviceNameIndex.TryGetValue(entry.ServiceName, out var serviceList))
        {
            serviceList.Remove(entry.Id);
            if (serviceList.Count == 0)
            {
                _serviceNameIndex.TryRemove(entry.ServiceName, out _);
            }
        }
    }

    private void RebuildIndexes()
    {
        lock (_indexLock)
        {
            _idIndex.Clear();
            _traceIdIndex.Clear();
            _requestIdIndex.Clear();
            _serviceNameIndex.Clear();

            foreach (var entry in _buffer)
            {
                _idIndex[entry.Id] = entry;

                if (!string.IsNullOrEmpty(entry.TraceId))
                {
                    var list = _traceIdIndex.GetOrAdd(entry.TraceId, _ => []);
                    list.Add(entry.Id);
                }

                if (!string.IsNullOrEmpty(entry.RequestId))
                {
                    var list = _requestIdIndex.GetOrAdd(entry.RequestId, _ => []);
                    list.Add(entry.Id);
                }

                if (!string.IsNullOrEmpty(entry.ServiceName))
                {
                    var list = _serviceNameIndex.GetOrAdd(entry.ServiceName, _ => []);
                    list.Add(entry.Id);
                }
            }
        }
    }

    private static IReadOnlyList<LogEntry> ApplyFilters(IReadOnlyList<LogEntry> entries, LogQuery query)
    {
        var filtered = entries.AsEnumerable();

        // 时间过滤
        if (query.StartTime.HasValue)
        {
            filtered = filtered.Where(e => e.Timestamp >= query.StartTime.Value);
        }

        if (query.EndTime.HasValue)
        {
            filtered = filtered.Where(e => e.Timestamp <= query.EndTime.Value);
        }

        // 级别过滤
        if (query.MinLevel.HasValue)
        {
            filtered = filtered.Where(e => e.Level >= query.MinLevel.Value);
        }

        if (query.Levels is { Count: > 0 })
        {
            var levels = query.Levels.ToHashSet();
            filtered = filtered.Where(e => levels.Contains(e.Level));
        }

        // 文本搜索
        if (!string.IsNullOrEmpty(query.SearchText))
        {
            var searchText = query.CaseSensitive ? query.SearchText : query.SearchText.ToLowerInvariant();
            filtered = filtered.Where(e =>
            {
                if (query.SearchInMessage && !string.IsNullOrEmpty(e.Message))
                {
                    var message = query.CaseSensitive ? e.Message : e.Message.ToLowerInvariant();
                    if (message.Contains(searchText))
                        return true;
                }

                if (query.SearchInException && !string.IsNullOrEmpty(e.Exception))
                {
                    var exception = query.CaseSensitive ? e.Exception : e.Exception.ToLowerInvariant();
                    if (exception.Contains(searchText))
                        return true;
                }

                return false;
            });
        }

        // 服务名称过滤
        if (!string.IsNullOrEmpty(query.ServiceName))
        {
            filtered = filtered.Where(e => e.ServiceName == query.ServiceName);
        }

        // 类别过滤
        if (!string.IsNullOrEmpty(query.Category))
        {
            filtered = filtered.Where(e => e.Category.Contains(query.Category, StringComparison.OrdinalIgnoreCase));
        }

        // 事件 ID 过滤
        if (query.EventId.HasValue)
        {
            filtered = filtered.Where(e => e.EventId == query.EventId.Value);
        }

        // 来源过滤
        if (query.Source.HasValue)
        {
            filtered = filtered.Where(e => e.Source == query.Source.Value);
        }

        // TraceId 过滤
        if (!string.IsNullOrEmpty(query.TraceId))
        {
            filtered = filtered.Where(e => e.TraceId == query.TraceId);
        }

        // RequestId 过滤
        if (!string.IsNullOrEmpty(query.RequestId))
        {
            filtered = filtered.Where(e => e.RequestId == query.RequestId);
        }

        // 请求路径过滤
        if (!string.IsNullOrEmpty(query.RequestPath))
        {
            if (query.RequestPath.Contains('*'))
            {
                var pattern = query.RequestPath.Replace("*", ".*");
                filtered = filtered.Where(e =>
                    !string.IsNullOrEmpty(e.RequestPath) &&
                    System.Text.RegularExpressions.Regex.IsMatch(e.RequestPath, pattern));
            }
            else
            {
                filtered = filtered.Where(e => e.RequestPath == query.RequestPath);
            }
        }

        // 请求方法过滤
        if (!string.IsNullOrEmpty(query.RequestMethod))
        {
            filtered = filtered.Where(e => e.RequestMethod == query.RequestMethod);
        }

        // 状态码范围过滤
        if (query.StatusCodeMin.HasValue)
        {
            filtered = filtered.Where(e => e.StatusCode >= query.StatusCodeMin.Value);
        }

        if (query.StatusCodeMax.HasValue)
        {
            filtered = filtered.Where(e => e.StatusCode <= query.StatusCodeMax.Value);
        }

        // 客户端 IP 过滤
        if (!string.IsNullOrEmpty(query.ClientIp))
        {
            filtered = filtered.Where(e => e.ClientIp == query.ClientIp);
        }

        // 用户 ID 过滤
        if (!string.IsNullOrEmpty(query.UserId))
        {
            filtered = filtered.Where(e => e.UserId == query.UserId);
        }

        // 标签过滤
        if (query.Tags is { Count: > 0 })
        {
            var tags = query.Tags.ToHashSet();
            filtered = filtered.Where(e => e.Tags?.Any(t => tags.Contains(t)) == true);
        }

        return filtered.ToList();
    }

    private static IReadOnlyList<LogEntry> ApplySorting(IReadOnlyList<LogEntry> entries, LogQuery query)
    {
        var ordered = query.SortBy switch
        {
            LogSortField.Level => query.Descending
                ? entries.OrderByDescending(e => e.Level)
                : entries.OrderBy(e => e.Level),
            _ => query.Descending
                ? entries.OrderByDescending(e => e.Timestamp)
                : entries.OrderBy(e => e.Timestamp)
        };

        return ordered.ToList();
    }

    private static bool MatchesFilter(LogEntry entry, LogQuery query)
    {
        if (query.StartTime.HasValue && entry.Timestamp < query.StartTime.Value)
            return false;

        if (query.EndTime.HasValue && entry.Timestamp > query.EndTime.Value)
            return false;

        if (query.MinLevel.HasValue && entry.Level < query.MinLevel.Value)
            return false;

        if (query.Levels is { Count: > 0 } && !query.Levels.Contains(entry.Level))
            return false;

        if (!string.IsNullOrEmpty(query.ServiceName) && entry.ServiceName != query.ServiceName)
            return false;

        if (!string.IsNullOrEmpty(query.TraceId) && entry.TraceId != query.TraceId)
            return false;

        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _cleanupTimer?.Dispose();
        _streamChannel.Writer.Complete();
    }
}
