using System.Collections.Concurrent;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Telemetry.Logging;

/// <summary>
/// 内存日志存储 (用于 Dashboard 实时查看)
/// </summary>
public sealed class InMemoryLogStore(int _maxSize = 10000)
{
    private readonly ConcurrentQueue<LogEntry> _logs = new();
    private long _totalCount;

    /// <summary>
    /// 添加日志条目
    /// </summary>
    public void Add(LogEntry entry)
    {
        _logs.Enqueue(entry);
        Interlocked.Increment(ref _totalCount);

        // 保持队列大小
        while (_logs.Count > _maxSize)
        {
            _logs.TryDequeue(out _);
        }
    }

    /// <summary>
    /// 查询日志
    /// </summary>
    public LogQueryResult Query(LogQuery query)
    {
        var logs = _logs.ToArray().AsEnumerable();

        // 应用过滤器
        if (query.MinLevel.HasValue)
        {
            logs = logs.Where(l => l.Level >= query.MinLevel.Value);
        }

        if (!string.IsNullOrEmpty(query.Category))
        {
            logs = logs.Where(l => l.Category.Contains(query.Category, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(query.SearchText))
        {
            logs = logs.Where(l => l.Message.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(query.ServiceName))
        {
            logs = logs.Where(l => l.ServiceName == query.ServiceName);
        }

        if (!string.IsNullOrEmpty(query.TraceId))
        {
            logs = logs.Where(l => l.TraceId == query.TraceId);
        }

        if (query.StartTime.HasValue)
        {
            logs = logs.Where(l => l.Timestamp >= query.StartTime.Value);
        }

        if (query.EndTime.HasValue)
        {
            logs = logs.Where(l => l.Timestamp <= query.EndTime.Value);
        }

        // 转换为数组计算总数
        var filtered = logs.ToArray();
        var totalCount = filtered.Length;

        // 排序
        filtered = query.Descending
            ? [.. filtered.OrderByDescending(l => l.Timestamp)]
            : [.. filtered.OrderBy(l => l.Timestamp)];

        // 分页
        var skip = query.Skip ?? 0;
        var take = query.Take ?? 100;
        var pagedLogs = filtered.Skip(skip).Take(take).ToList();

        return new()
        {
            Logs = pagedLogs,
            TotalCount = totalCount,
            TotalLogsEverReceived = Interlocked.Read(ref _totalCount),
        };
    }

    /// <summary>
    /// 获取最新的日志 (用于实时流)
    /// </summary>
    public IReadOnlyList<LogEntry> GetLatest(int count = 100) => _logs.TakeLast(count).Reverse().ToList();

    /// <summary>
    /// 清空日志
    /// </summary>
    public void Clear()
    {
        while (_logs.TryDequeue(out _)) { }
    }

    /// <summary>
    /// 获取统计信息
    /// </summary>
    public LogStatistics GetStatistics()
    {
        var logs = _logs.ToArray();
        return new()
        {
            TotalCount = logs.Length,
            TotalEverReceived = Interlocked.Read(ref _totalCount),
            ByLevel = logs.GroupBy(l => l.Level).ToDictionary(g => g.Key.ToString(), g => g.Count()),
            LastLogTime = logs.MaxBy(l => l.Timestamp)?.Timestamp,
        };
    }
}
