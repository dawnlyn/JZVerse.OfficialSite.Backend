using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.Gateway.Abstractions;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Gateway.Core;

/// <summary>
/// 审计日志记录器实现
/// </summary>
public sealed class AuditLogger : IAuditLogger
{
    private readonly ILogger<AuditLogger> _logger;
    private readonly IAuditLogStore _store;

    public AuditLogger(ILogger<AuditLogger> logger, IAuditLogStore store)
    {
        _logger = logger;
        _store = store;
    }

    public async ValueTask LogAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            await _store.SaveAsync(entry, cancellationToken);

            _logger.LogInformation(
                "审计日志: {Method} {Path} -> {StatusCode} ({Duration}ms) [Route: {RouteId}]",
                entry.Method,
                entry.RequestPath,
                entry.StatusCode,
                entry.DurationMs,
                entry.RouteId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存审计日志失败");
        }
    }
}

/// <summary>
/// 内存审计日志存储实现
/// </summary>
public sealed class InMemoryAuditLogStore : IAuditLogStore
{
    private readonly ConcurrentQueue<AuditLogEntry> _logs = new();
    private readonly int _maxSize;
    private readonly object _trimLock = new();

    public InMemoryAuditLogStore(int maxSize = 10000)
    {
        _maxSize = maxSize;
    }

    public Task SaveAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        _logs.Enqueue(entry);

        // 超过最大容量时删除旧条目
        if (_logs.Count > _maxSize)
        {
            lock (_trimLock)
            {
                while (_logs.Count > _maxSize * 0.9) // 保留 90%
                {
                    _logs.TryDequeue(out _);
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditLogEntry>> QueryAsync(
        AuditLogQuery query,
        CancellationToken cancellationToken = default)
    {
        var results = _logs.AsEnumerable();

        if (query.From.HasValue)
        {
            results = results.Where(l => l.Timestamp >= query.From.Value);
        }

        if (query.To.HasValue)
        {
            results = results.Where(l => l.Timestamp <= query.To.Value);
        }

        if (!string.IsNullOrEmpty(query.UserId))
        {
            results = results.Where(l => l.UserId == query.UserId);
        }

        if (!string.IsNullOrEmpty(query.ClientIp))
        {
            results = results.Where(l => l.ClientIp == query.ClientIp);
        }

        if (!string.IsNullOrEmpty(query.RouteId))
        {
            results = results.Where(l => l.RouteId == query.RouteId);
        }

        if (!string.IsNullOrEmpty(query.TargetService))
        {
            results = results.Where(l => l.TargetService == query.TargetService);
        }

        if (query.StatusCode.HasValue)
        {
            results = results.Where(l => l.StatusCode == query.StatusCode.Value);
        }

        if (query.MinStatusCode.HasValue)
        {
            results = results.Where(l => l.StatusCode >= query.MinStatusCode.Value);
        }

        if (query.MaxStatusCode.HasValue)
        {
            results = results.Where(l => l.StatusCode <= query.MaxStatusCode.Value);
        }

        if (query.OnlyErrors)
        {
            results = results.Where(l => l.StatusCode >= 400);
        }

        var list = results
            .OrderByDescending(l => l.Timestamp)
            .Skip(query.Skip)
            .Take(query.Take)
            .ToList();

        return Task.FromResult<IReadOnlyList<AuditLogEntry>>(list);
    }

    public Task<AuditLogStatistics> GetStatisticsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var logs = _logs.Where(l => l.Timestamp >= from && l.Timestamp <= to).ToList();

        if (logs.Count == 0)
        {
            return Task.FromResult(new AuditLogStatistics());
        }

        var durations = logs.Select(l => l.DurationMs).OrderBy(d => d).ToList();

        var stats = new AuditLogStatistics
        {
            TotalRequests = logs.Count,
            SuccessfulRequests = logs.Count(l => l.StatusCode is >= 200 and < 300),
            ClientErrors = logs.Count(l => l.StatusCode is >= 400 and < 500),
            ServerErrors = logs.Count(l => l.StatusCode >= 500),
            AverageResponseTimeMs = durations.Average(),
            P50ResponseTimeMs = GetPercentile(durations, 0.50),
            P95ResponseTimeMs = GetPercentile(durations, 0.95),
            P99ResponseTimeMs = GetPercentile(durations, 0.99),
            RateLimitedRequests = logs.Count(l => l.WasRateLimited),
            CacheHits = logs.Count(l => l.WasCacheHit),
            CircuitBrokenRequests = logs.Count(l => l.WasCircuitBroken),
            RequestsByService = logs
                .Where(l => !string.IsNullOrEmpty(l.TargetService))
                .GroupBy(l => l.TargetService!)
                .ToDictionary(g => g.Key, g => (long)g.Count()),
            RequestsByRoute = logs
                .Where(l => !string.IsNullOrEmpty(l.RouteId))
                .GroupBy(l => l.RouteId!)
                .ToDictionary(g => g.Key, g => (long)g.Count())
        };

        return Task.FromResult(stats);
    }

    private static double GetPercentile(List<long> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0) return 0;

        var index = (int)Math.Ceiling(percentile * sortedValues.Count) - 1;
        return sortedValues[Math.Max(0, index)];
    }
}
