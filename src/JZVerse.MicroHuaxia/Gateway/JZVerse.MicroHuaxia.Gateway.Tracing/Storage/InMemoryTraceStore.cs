using System.Collections.Concurrent;
using System.Diagnostics;
using JZVerse.MicroHuaxia.Gateway.Tracing.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Gateway.Tracing.Storage;

/// <summary>
/// 内存追踪存储实现
/// </summary>
public sealed class InMemoryTraceStore : ITraceStore, IDisposable
{
    private readonly ConcurrentDictionary<string, List<TraceSpan>> _traces = new();
    private readonly ConcurrentQueue<(string TraceId, DateTimeOffset AddedAt)> _traceOrder = new();
    private readonly InMemoryStoreOptions _options;
    private readonly ILogger<InMemoryTraceStore> _logger;
    private readonly Timer _cleanupTimer;
    private readonly object _cleanupLock = new();
    private long _totalSpanCount;
    private bool _disposed;

    public InMemoryTraceStore(IOptions<GatewayTracingOptions> options, ILogger<InMemoryTraceStore> logger)
    {
        _options = options.Value.InMemoryStore;
        _logger = logger;

        // 启动定期清理任务
        var cleanupInterval = TimeSpan.FromMinutes(_options.CleanupIntervalMinutes);
        _cleanupTimer = new Timer(CleanupCallback, null, cleanupInterval, cleanupInterval);
    }

    public void AddSpan(TraceSpan span)
    {
        if (_disposed)
            return;

        var spans = _traces.GetOrAdd(span.TraceId, _ => []);

        lock (spans)
        {
            spans.Add(span);
        }

        Interlocked.Increment(ref _totalSpanCount);

        // 记录追踪顺序（仅在首次添加时）
        if (spans.Count == 1)
        {
            _traceOrder.Enqueue((span.TraceId, DateTimeOffset.UtcNow));
        }

        // 检查是否需要清理
        if (_totalSpanCount > _options.MaxSpans)
        {
            TryCleanup();
        }
    }

    public void AddSpans(IEnumerable<TraceSpan> spans)
    {
        foreach (var span in spans)
        {
            AddSpan(span);
        }
    }

    public IReadOnlyList<TraceSpan> GetTrace(string traceId)
    {
        if (_traces.TryGetValue(traceId, out var spans))
        {
            lock (spans)
            {
                return spans.OrderBy(s => s.StartTime).ToList();
            }
        }

        return [];
    }

    public TraceSpan? GetSpan(string traceId, string spanId)
    {
        if (_traces.TryGetValue(traceId, out var spans))
        {
            lock (spans)
            {
                return spans.FirstOrDefault(s => s.SpanId == spanId);
            }
        }

        return null;
    }

    public TraceQueryResult Query(TraceQuery query)
    {
        var summaries = GetAllTraceSummaries();

        // 应用过滤条件
        var filtered = summaries.AsEnumerable();

        if (!string.IsNullOrEmpty(query.ServiceName))
        {
            filtered = filtered.Where(t => t.Services.Contains(query.ServiceName, StringComparer.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(query.OperationName))
        {
            filtered = filtered.Where(t =>
                t.RootSpanName?.Contains(query.OperationName, StringComparison.OrdinalIgnoreCase) == true
            );
        }

        if (query.StartTimeFrom.HasValue)
        {
            filtered = filtered.Where(t => t.StartTime >= query.StartTimeFrom.Value);
        }

        if (query.StartTimeTo.HasValue)
        {
            filtered = filtered.Where(t => t.StartTime <= query.StartTimeTo.Value);
        }

        if (query.MinDuration.HasValue)
        {
            filtered = filtered.Where(t => t.Duration >= query.MinDuration.Value);
        }

        if (query.MaxDuration.HasValue)
        {
            filtered = filtered.Where(t => t.Duration <= query.MaxDuration.Value);
        }

        if (query.HasError.HasValue)
        {
            filtered = filtered.Where(t => t.HasError == query.HasError.Value);
        }

        if (query.HttpStatusCode.HasValue)
        {
            filtered = filtered.Where(t => t.HttpStatusCode == query.HttpStatusCode.Value);
        }

        // 应用属性过滤
        if (query.Attributes.Count > 0)
        {
            foreach (var (key, value) in query.Attributes)
            {
                filtered = filtered.Where(t =>
                {
                    var spans = GetTrace(t.TraceId);
                    return spans.Any(s =>
                        s.Attributes.TryGetValue(key, out var attrValue)
                        && attrValue?.ToString()?.Contains(value, StringComparison.OrdinalIgnoreCase) == true
                    );
                });
            }
        }

        // 排序
        filtered = query.SortBy switch
        {
            TraceQuerySortField.Duration => query.SortDescending
                ? filtered.OrderByDescending(t => t.Duration)
                : filtered.OrderBy(t => t.Duration),
            TraceQuerySortField.SpanCount => query.SortDescending
                ? filtered.OrderByDescending(t => t.SpanCount)
                : filtered.OrderBy(t => t.SpanCount),
            _ => query.SortDescending
                ? filtered.OrderByDescending(t => t.StartTime)
                : filtered.OrderBy(t => t.StartTime),
        };

        var total = filtered.Count();
        var results = filtered.Skip(query.Skip).Take(Math.Min(query.Take, 100)).ToList();

        return new TraceQueryResult
        {
            Traces = results,
            TotalCount = total,
            HasMore = query.Skip + results.Count < total,
        };
    }

    public TraceStatistics GetStatistics()
    {
        var summaries = GetAllTraceSummaries();
        var durations = summaries.Select(s => s.Duration.TotalMilliseconds).OrderBy(d => d).ToList();

        return new TraceStatistics
        {
            TotalTraces = summaries.Count,
            TotalSpans = _totalSpanCount,
            ErrorTraces = summaries.Count(s => s.HasError),
            AverageDurationMs = durations.Count > 0 ? durations.Average() : 0,
            P50DurationMs = GetPercentile(durations, 50),
            P95DurationMs = GetPercentile(durations, 95),
            P99DurationMs = GetPercentile(durations, 99),
            TracesByService = summaries
                .SelectMany(s => s.Services)
                .GroupBy(s => s)
                .ToDictionary(g => g.Key, g => (long)g.Count()),
            TracesByOperation = summaries
                .Where(s => s.RootSpanName != null)
                .GroupBy(s => s.RootSpanName!)
                .ToDictionary(g => g.Key, g => (long)g.Count()),
        };
    }

    public IReadOnlyList<TraceSummary> GetLatestTraces(int count = 20)
    {
        return GetAllTraceSummaries()
            .OrderByDescending(t => t.StartTime)
            .Take(count)
            .ToList();
    }

    public void Cleanup()
    {
        lock (_cleanupLock)
        {
            var cutoff = DateTimeOffset.UtcNow.AddMinutes(-_options.RetentionMinutes);
            var tracesToRemove = new List<string>();

            // 找出过期的追踪
            foreach (var (traceId, spans) in _traces)
            {
                lock (spans)
                {
                    if (spans.Count > 0 && spans.All(s => s.StartTime < cutoff))
                    {
                        tracesToRemove.Add(traceId);
                    }
                }
            }

            // 删除过期追踪
            foreach (var traceId in tracesToRemove)
            {
                if (_traces.TryRemove(traceId, out var spans))
                {
                    Interlocked.Add(ref _totalSpanCount, -spans.Count);
                }
            }

            if (tracesToRemove.Count > 0)
            {
                _logger.LogDebug("Cleaned up {Count} expired traces", tracesToRemove.Count);
            }

            // 如果仍然超过限制，删除最旧的追踪
            while (_totalSpanCount > _options.MaxSpans && _traceOrder.TryDequeue(out var oldest))
            {
                if (_traces.TryRemove(oldest.TraceId, out var spans))
                {
                    Interlocked.Add(ref _totalSpanCount, -spans.Count);
                }
            }
        }
    }

    public void Clear()
    {
        _traces.Clear();
        while (_traceOrder.TryDequeue(out _))
        {
        }
        Interlocked.Exchange(ref _totalSpanCount, 0);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cleanupTimer.Dispose();
    }

    private void CleanupCallback(object? state)
    {
        try
        {
            Cleanup();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during trace cleanup");
        }
    }

    private void TryCleanup()
    {
        // 避免频繁清理
        if (Monitor.TryEnter(_cleanupLock, TimeSpan.Zero))
        {
            try
            {
                Cleanup();
            }
            finally
            {
                Monitor.Exit(_cleanupLock);
            }
        }
    }

    private List<TraceSummary> GetAllTraceSummaries()
    {
        var summaries = new List<TraceSummary>();

        foreach (var (traceId, spans) in _traces)
        {
            lock (spans)
            {
                if (spans.Count == 0)
                    continue;

                var orderedSpans = spans.OrderBy(s => s.StartTime).ToList();
                var rootSpan = orderedSpans.FirstOrDefault(s => s.ParentSpanId == null) ?? orderedSpans[0];
                var lastSpan = orderedSpans.MaxBy(s => s.EndTime ?? s.StartTime);

                var endTime = lastSpan?.EndTime ?? orderedSpans.Max(s => s.EndTime ?? s.StartTime);
                var duration = endTime - rootSpan.StartTime;

                // 获取 HTTP 状态码
                int? httpStatusCode = null;
                var statusCodeAttr = orderedSpans
                    .SelectMany(s => s.Attributes)
                    .FirstOrDefault(a => a.Key == GatewaySpanAttributes.HttpStatusCode);
                if (statusCodeAttr.Value is int code)
                {
                    httpStatusCode = code;
                }
                else if (statusCodeAttr.Value is string codeStr && int.TryParse(codeStr, out var parsedCode))
                {
                    httpStatusCode = parsedCode;
                }

                summaries.Add(
                    new TraceSummary
                    {
                        TraceId = traceId,
                        RootSpanName = rootSpan.Name,
                        StartTime = rootSpan.StartTime,
                        Duration = duration,
                        SpanCount = spans.Count,
                        Services = spans.Select(s => s.ServiceName).Distinct().ToList(),
                        HasError = spans.Any(s => s.Status == ActivityStatusCode.Error),
                        HttpStatusCode = httpStatusCode,
                    }
                );
            }
        }

        return summaries;
    }

    private static double GetPercentile(List<double> sortedValues, int percentile)
    {
        if (sortedValues.Count == 0)
            return 0;

        var index = (int)Math.Ceiling(percentile / 100.0 * sortedValues.Count) - 1;
        return sortedValues[Math.Max(0, Math.Min(index, sortedValues.Count - 1))];
    }
}
