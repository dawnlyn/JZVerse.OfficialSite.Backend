using System.Runtime.CompilerServices;
using JZVerse.MicroHuaxia.Gateway.Logging.Storage;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Gateway.Logging.Replay;

/// <summary>
/// 日志回放服务实现
/// </summary>
public sealed class LogReplayService : ILogReplayService
{
    private readonly ILogStore _logStore;
    private readonly IStreamableLogStore? _streamableStore;
    private readonly ILogger<LogReplayService> _logger;

    /// <summary>
    /// 创建日志回放服务
    /// </summary>
    public LogReplayService(
        ILogStore logStore,
        ILogger<LogReplayService> logger)
    {
        _logStore = logStore;
        _streamableStore = logStore as IStreamableLogStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<LogQueryResult> ReplayAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        return _logStore.QueryAsync(query, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TraceReplayResult> ReplayByTraceAsync(string traceId, CancellationToken cancellationToken = default)
    {
        var entries = await _logStore.GetByTraceIdAsync(traceId, cancellationToken);

        if (entries.Count == 0)
        {
            return new TraceReplayResult
            {
                TraceId = traceId,
                Entries = []
            };
        }

        var orderedEntries = entries.OrderBy(e => e.Timestamp).ToList();
        var startTime = orderedEntries.First().Timestamp;
        var endTime = orderedEntries.Last().Timestamp;
        var totalDuration = (endTime - startTime).TotalMilliseconds;

        var services = orderedEntries
            .Where(e => !string.IsNullOrEmpty(e.ServiceName))
            .Select(e => e.ServiceName!)
            .Distinct()
            .ToList();

        var hasErrors = orderedEntries.Any(e =>
            e.Level is LogLevel.Error or LogLevel.Critical);

        // 分析请求阶段
        var phases = AnalyzePhases(orderedEntries);

        return new TraceReplayResult
        {
            TraceId = traceId,
            Entries = orderedEntries,
            StartTime = startTime,
            EndTime = endTime,
            TotalDurationMs = totalDuration,
            Services = services,
            HasErrors = hasErrors,
            Phases = phases
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LogEntry>> ReplayByRequestAsync(string requestId, CancellationToken cancellationToken = default)
    {
        var query = new LogQuery
        {
            RequestId = requestId,
            Take = 1000,
            Descending = false
        };

        var result = await _logStore.QueryAsync(query, cancellationToken);
        return result.Entries;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SlowQueryInfo>> AnalyzeSlowQueriesAsync(
        int thresholdMs,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default)
    {
        // 查询指定时间范围内的所有日志
        var query = new LogQuery
        {
            StartTime = start,
            EndTime = end,
            Take = 10000
        };

        var result = await _logStore.QueryAsync(query, cancellationToken);

        // 筛选有持续时间的日志
        var entriesWithDuration = result.Entries
            .Where(e => e.DurationMs.HasValue && e.DurationMs.Value >= thresholdMs)
            .Where(e => !string.IsNullOrEmpty(e.RequestPath))
            .ToList();

        // 按请求路径分组分析
        var grouped = entriesWithDuration
            .GroupBy(e => new { e.RequestPath, e.RequestMethod })
            .Select(g =>
            {
                var durations = g.Select(e => e.DurationMs!.Value).OrderBy(d => d).ToList();
                var count = durations.Count;

                return new SlowQueryInfo
                {
                    RequestPath = g.Key.RequestPath!,
                    RequestMethod = g.Key.RequestMethod,
                    Count = count,
                    AvgDurationMs = durations.Average(),
                    MinDurationMs = durations.First(),
                    MaxDurationMs = durations.Last(),
                    P95DurationMs = GetPercentile(durations, 0.95),
                    P99DurationMs = GetPercentile(durations, 0.99),
                    SampleTraceIds = g
                        .Where(e => !string.IsNullOrEmpty(e.TraceId))
                        .Take(5)
                        .Select(e => e.TraceId!)
                        .ToList()
                };
            })
            .OrderByDescending(s => s.AvgDurationMs)
            .ToList();

        return grouped;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<LogEntry> StreamLogsAsync(
        LogQuery? filter = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_streamableStore is null)
        {
            _logger.LogWarning("当前存储不支持实时流");
            yield break;
        }

        await foreach (var entry in _streamableStore.StreamAsync(filter, cancellationToken))
        {
            yield return entry;
        }
    }

    private static IReadOnlyList<RequestPhase> AnalyzePhases(IReadOnlyList<LogEntry> entries)
    {
        var phases = new List<RequestPhase>();

        // 按服务分组
        var byService = entries
            .Where(e => !string.IsNullOrEmpty(e.ServiceName))
            .GroupBy(e => e.ServiceName!)
            .ToList();

        foreach (var serviceGroup in byService)
        {
            var serviceEntries = serviceGroup.OrderBy(e => e.Timestamp).ToList();
            var startTime = serviceEntries.First().Timestamp;
            var endTime = serviceEntries.Last().Timestamp;

            phases.Add(new RequestPhase
            {
                Name = serviceGroup.Key,
                ServiceName = serviceGroup.Key,
                StartTime = startTime,
                EndTime = endTime,
                DurationMs = (endTime - startTime).TotalMilliseconds,
                Entries = serviceEntries
            });
        }

        return phases.OrderBy(p => p.StartTime).ToList();
    }

    private static double GetPercentile(List<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0)
            return 0;

        var index = (int)Math.Ceiling(percentile * sortedValues.Count) - 1;
        return sortedValues[Math.Max(0, Math.Min(index, sortedValues.Count - 1))];
    }
}
