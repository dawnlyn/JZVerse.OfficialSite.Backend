using JZVerse.MicroHuaxia.Gateway.Metrics.Api.Models;
using JZVerse.MicroHuaxia.Gateway.Metrics.Storage;

namespace JZVerse.MicroHuaxia.Gateway.Metrics.Api;

/// <summary>
/// 网关指标数据提供者实现
/// </summary>
public sealed class GatewayMetricsProvider : IGatewayMetricsProvider
{
    private readonly IMetricsStore _store;

    /// <summary>
    /// 创建网关指标数据提供者
    /// </summary>
    public GatewayMetricsProvider(IMetricsStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public Task<MetricsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var start = now.AddMinutes(-5); // 最近 5 分钟的快照

        // 获取请求指标
        var requestAggregations = _store.GetAggregations(
            GatewayMetricNames.RequestsTotal,
            start,
            now,
            TimeSpan.FromMinutes(5)
        );

        var durationAggregations = _store.GetAggregations(
            GatewayMetricNames.RequestDuration,
            start,
            now,
            TimeSpan.FromMinutes(5)
        );

        var totalRequests = requestAggregations.Sum(a => (long)a.Sum);
        var avgDuration = durationAggregations.Count > 0 ? durationAggregations.Average(a => a.Average) : 0;
        var p50Duration = durationAggregations.Count > 0 ? durationAggregations.Average(a => a.P50) : 0;
        var p95Duration = durationAggregations.Count > 0 ? durationAggregations.Average(a => a.P95) : 0;
        var p99Duration = durationAggregations.Count > 0 ? durationAggregations.Average(a => a.P99) : 0;

        // 获取错误指标
        var errorAggregations = _store.GetAggregations(
            GatewayMetricNames.ErrorsTotal,
            start,
            now,
            TimeSpan.FromMinutes(5)
        );
        var totalErrors = errorAggregations.Sum(a => (long)a.Sum);
        var errorRate = totalRequests > 0 ? (double)totalErrors / totalRequests : 0;

        // 获取缓存指标
        var cacheHitAggregations = _store.GetAggregations(
            GatewayMetricNames.CacheHits,
            start,
            now,
            TimeSpan.FromMinutes(5)
        );
        var cacheMissAggregations = _store.GetAggregations(
            GatewayMetricNames.CacheMisses,
            start,
            now,
            TimeSpan.FromMinutes(5)
        );
        var cacheHits = cacheHitAggregations.Sum(a => (long)a.Sum);
        var cacheMisses = cacheMissAggregations.Sum(a => (long)a.Sum);
        var cacheTotal = cacheHits + cacheMisses;
        var cacheHitRate = cacheTotal > 0 ? (double)cacheHits / cacheTotal : 0;

        // 获取限流指标
        var rateLimitAllowedAggregations = _store.GetAggregations(
            GatewayMetricNames.RateLimitAllowed,
            start,
            now,
            TimeSpan.FromMinutes(5)
        );
        var rateLimitBlockedAggregations = _store.GetAggregations(
            GatewayMetricNames.RateLimitBlocked,
            start,
            now,
            TimeSpan.FromMinutes(5)
        );
        var rateLimitAllowed = rateLimitAllowedAggregations.Sum(a => (long)a.Sum);
        var rateLimitBlocked = rateLimitBlockedAggregations.Sum(a => (long)a.Sum);
        var rateLimitTotal = rateLimitAllowed + rateLimitBlocked;
        var rateLimitBlockedRate = rateLimitTotal > 0 ? (double)rateLimitBlocked / rateLimitTotal : 0;

        // 获取状态码分布
        var requestsByStatusGroup = new Dictionary<string, long>();
        foreach (var agg in requestAggregations)
        {
            if (agg.Tags.TryGetValue(GatewayMetricTags.StatusCodeGroup, out var group) && group != null)
            {
                var key = group.ToString()!;
                requestsByStatusGroup[key] = requestsByStatusGroup.GetValueOrDefault(key) + (long)agg.Sum;
            }
        }

        // 获取 Top 路由
        var topRoutesByRequests = new Dictionary<string, long>();
        foreach (var agg in requestAggregations)
        {
            if (agg.Tags.TryGetValue(GatewayMetricTags.RouteId, out var routeId) && routeId != null)
            {
                var key = routeId.ToString()!;
                topRoutesByRequests[key] = topRoutesByRequests.GetValueOrDefault(key) + (long)agg.Sum;
            }
        }

        var snapshot = new MetricsSnapshot
        {
            Timestamp = now,
            TotalRequests = totalRequests,
            ActiveRequests = 0, // 需要从 GatewayMetrics 的 Provider 获取
            AverageResponseTimeMs = avgDuration,
            P50ResponseTimeMs = p50Duration,
            P95ResponseTimeMs = p95Duration,
            P99ResponseTimeMs = p99Duration,
            ErrorRate = errorRate,
            CacheHitRate = cacheHitRate,
            RateLimitBlockedRate = rateLimitBlockedRate,
            CircuitBreakerOpenCount = 0, // 需要从 GatewayMetrics 的 Provider 获取
            RequestsByStatusGroup = requestsByStatusGroup,
            TopRoutesByRequests = topRoutesByRequests
                .OrderByDescending(kv => kv.Value)
                .Take(10)
                .ToDictionary(kv => kv.Key, kv => kv.Value),
        };

        return Task.FromResult(snapshot);
    }

    /// <inheritdoc />
    public Task<MetricTimeSeries> GetTimeSeriesAsync(
        string metricName,
        DateTimeOffset? start = null,
        DateTimeOffset? end = null,
        Dictionary<string, string>? tags = null,
        int? bucketSizeSeconds = null,
        CancellationToken cancellationToken = default
    )
    {
        var endTime = end ?? DateTimeOffset.UtcNow;
        var startTime = start ?? endTime.AddHours(-1);
        var bucketSize = TimeSpan.FromSeconds(bucketSizeSeconds ?? 60);

        IReadOnlyDictionary<string, object?>? tagsDict = tags?.ToDictionary(
            kv => kv.Key,
            kv => (object?)kv.Value
        );

        var aggregations = _store.GetAggregations(metricName, startTime, endTime, bucketSize, tagsDict);

        var dataPoints = aggregations
            .Select(a => new TimeSeriesDataPoint { Timestamp = a.StartTime, Value = a.Sum })
            .OrderBy(p => p.Timestamp)
            .ToList();

        var result = new MetricTimeSeries
        {
            MetricName = metricName,
            Tags = tagsDict ?? new Dictionary<string, object?>(),
            DataPoints = dataPoints,
            Aggregation = AggregationType.Sum,
        };

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<MetricsStatistics> GetStatisticsAsync(
        DateTimeOffset? start = null,
        DateTimeOffset? end = null,
        CancellationToken cancellationToken = default
    )
    {
        var endTime = end ?? DateTimeOffset.UtcNow;
        var startTime = start ?? endTime.AddHours(-1);
        var bucketSize = TimeSpan.FromMinutes(5);

        // 请求统计
        var requestAggregations = _store.GetAggregations(
            GatewayMetricNames.RequestsTotal,
            startTime,
            endTime,
            bucketSize
        );
        var durationAggregations = _store.GetAggregations(
            GatewayMetricNames.RequestDuration,
            startTime,
            endTime,
            bucketSize
        );
        var errorAggregations = _store.GetAggregations(
            GatewayMetricNames.ErrorsTotal,
            startTime,
            endTime,
            bucketSize
        );

        var totalRequests = requestAggregations.Sum(a => (long)a.Sum);
        var totalErrors = errorAggregations.Sum(a => (long)a.Sum);

        var requestStats = new RequestStatistics
        {
            Total = totalRequests,
            Success = totalRequests - totalErrors,
            Errors = totalErrors,
            AverageResponseTimeMs =
                durationAggregations.Count > 0 ? durationAggregations.Average(a => a.Average) : 0,
            P50ResponseTimeMs = durationAggregations.Count > 0 ? durationAggregations.Average(a => a.P50) : 0,
            P95ResponseTimeMs = durationAggregations.Count > 0 ? durationAggregations.Average(a => a.P95) : 0,
            P99ResponseTimeMs = durationAggregations.Count > 0 ? durationAggregations.Average(a => a.P99) : 0,
            ByStatusCode = [],
            ByMethod = [],
        };

        // 认证统计
        var authAttemptAggregations = _store.GetAggregations(
            GatewayMetricNames.AuthAttempts,
            startTime,
            endTime,
            bucketSize
        );
        var authFailureAggregations = _store.GetAggregations(
            GatewayMetricNames.AuthFailures,
            startTime,
            endTime,
            bucketSize
        );

        var totalAuthAttempts = authAttemptAggregations.Sum(a => (long)a.Sum);
        var totalAuthFailures = authFailureAggregations.Sum(a => (long)a.Sum);

        var authStats = new AuthStatistics
        {
            TotalAttempts = totalAuthAttempts,
            Success = totalAuthAttempts - totalAuthFailures,
            Failures = totalAuthFailures,
            SuccessRate = totalAuthAttempts > 0 ? (double)(totalAuthAttempts - totalAuthFailures) / totalAuthAttempts : 0,
            AverageDurationMs = 0,
            ByStrategy = [],
        };

        // 限流统计
        var rateLimitAllowedAggregations = _store.GetAggregations(
            GatewayMetricNames.RateLimitAllowed,
            startTime,
            endTime,
            bucketSize
        );
        var rateLimitBlockedAggregations = _store.GetAggregations(
            GatewayMetricNames.RateLimitBlocked,
            startTime,
            endTime,
            bucketSize
        );

        var rateLimitAllowed = rateLimitAllowedAggregations.Sum(a => (long)a.Sum);
        var rateLimitBlocked = rateLimitBlockedAggregations.Sum(a => (long)a.Sum);
        var rateLimitTotal = rateLimitAllowed + rateLimitBlocked;

        var rateLimitStats = new RateLimitStatistics
        {
            Allowed = rateLimitAllowed,
            Blocked = rateLimitBlocked,
            BlockedRate = rateLimitTotal > 0 ? (double)rateLimitBlocked / rateLimitTotal : 0,
            ByAlgorithm = [],
        };

        // 缓存统计
        var cacheHitAggregations = _store.GetAggregations(
            GatewayMetricNames.CacheHits,
            startTime,
            endTime,
            bucketSize
        );
        var cacheMissAggregations = _store.GetAggregations(
            GatewayMetricNames.CacheMisses,
            startTime,
            endTime,
            bucketSize
        );
        var cacheEvictionAggregations = _store.GetAggregations(
            GatewayMetricNames.CacheEvictions,
            startTime,
            endTime,
            bucketSize
        );

        var cacheHits = cacheHitAggregations.Sum(a => (long)a.Sum);
        var cacheMisses = cacheMissAggregations.Sum(a => (long)a.Sum);
        var cacheTotal = cacheHits + cacheMisses;
        var cacheEvictions = cacheEvictionAggregations.Sum(a => (long)a.Sum);

        var cacheStats = new CacheStatistics
        {
            Hits = cacheHits,
            Misses = cacheMisses,
            HitRate = cacheTotal > 0 ? (double)cacheHits / cacheTotal : 0,
            CurrentSize = 0,
            Evictions = cacheEvictions,
        };

        // 转发统计
        var forwardAttemptAggregations = _store.GetAggregations(
            GatewayMetricNames.ForwardAttempts,
            startTime,
            endTime,
            bucketSize
        );
        var forwardFailureAggregations = _store.GetAggregations(
            GatewayMetricNames.ForwardFailures,
            startTime,
            endTime,
            bucketSize
        );
        var forwardDurationAggregations = _store.GetAggregations(
            GatewayMetricNames.ForwardDuration,
            startTime,
            endTime,
            bucketSize
        );

        var forwardAttempts = forwardAttemptAggregations.Sum(a => (long)a.Sum);
        var forwardFailures = forwardFailureAggregations.Sum(a => (long)a.Sum);

        var forwardStats = new ForwardStatistics
        {
            TotalAttempts = forwardAttempts,
            Success = forwardAttempts - forwardFailures,
            Failures = forwardFailures,
            SuccessRate = forwardAttempts > 0 ? (double)(forwardAttempts - forwardFailures) / forwardAttempts : 0,
            AverageDurationMs =
                forwardDurationAggregations.Count > 0 ? forwardDurationAggregations.Average(a => a.Average) : 0,
            ByProtocol = [],
        };

        // 弹性统计
        var retryAggregations = _store.GetAggregations(
            GatewayMetricNames.RetryAttempts,
            startTime,
            endTime,
            bucketSize
        );
        var circuitBreakerTripAggregations = _store.GetAggregations(
            GatewayMetricNames.CircuitBreakerTrips,
            startTime,
            endTime,
            bucketSize
        );
        var bulkheadRejectedAggregations = _store.GetAggregations(
            GatewayMetricNames.BulkheadRejected,
            startTime,
            endTime,
            bucketSize
        );
        var fallbackAggregations = _store.GetAggregations(
            GatewayMetricNames.FallbackExecutions,
            startTime,
            endTime,
            bucketSize
        );
        var timeoutAggregations = _store.GetAggregations(
            GatewayMetricNames.TimeoutsTotal,
            startTime,
            endTime,
            bucketSize
        );

        var resilienceStats = new ResilienceStatistics
        {
            RetryAttempts = retryAggregations.Sum(a => (long)a.Sum),
            CircuitBreakerTrips = circuitBreakerTripAggregations.Sum(a => (long)a.Sum),
            BulkheadRejections = bulkheadRejectedAggregations.Sum(a => (long)a.Sum),
            FallbackExecutions = fallbackAggregations.Sum(a => (long)a.Sum),
            Timeouts = timeoutAggregations.Sum(a => (long)a.Sum),
        };

        var statistics = new MetricsStatistics
        {
            StartTime = startTime,
            EndTime = endTime,
            Requests = requestStats,
            Auth = authStats,
            RateLimit = rateLimitStats,
            Cache = cacheStats,
            Forward = forwardStats,
            Resilience = resilienceStats,
            Store = _store.GetStatistics(),
        };

        return Task.FromResult(statistics);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<RouteMetrics>> GetTopRoutesAsync(
        string metricName,
        int limit = 10,
        CancellationToken cancellationToken = default
    )
    {
        var now = DateTimeOffset.UtcNow;
        var start = now.AddHours(-1);
        var bucketSize = TimeSpan.FromHours(1);

        var aggregations = _store.GetAggregations(metricName, start, now, bucketSize);

        var routeMetrics = new Dictionary<string, (long total, long errors, double duration, long cacheHits, long cacheMisses, long rateLimitBlocked)>();

        foreach (var agg in aggregations)
        {
            if (!agg.Tags.TryGetValue(GatewayMetricTags.RouteId, out var routeIdObj) || routeIdObj == null)
                continue;

            var routeId = routeIdObj.ToString()!;
            if (!routeMetrics.TryGetValue(routeId, out var current))
            {
                current = (0, 0, 0, 0, 0, 0);
            }

            routeMetrics[routeId] = (
                current.total + (long)agg.Sum,
                current.errors,
                current.duration,
                current.cacheHits,
                current.cacheMisses,
                current.rateLimitBlocked
            );
        }

        var result = routeMetrics
            .Select(kv => new RouteMetrics
            {
                RouteId = kv.Key,
                TotalRequests = kv.Value.total,
                Errors = kv.Value.errors,
                ErrorRate = kv.Value.total > 0 ? (double)kv.Value.errors / kv.Value.total : 0,
                AverageResponseTimeMs = kv.Value.duration,
                P95ResponseTimeMs = 0,
                CacheHitRate =
                    kv.Value.cacheHits + kv.Value.cacheMisses > 0
                        ? (double)kv.Value.cacheHits / (kv.Value.cacheHits + kv.Value.cacheMisses)
                        : 0,
                RateLimitBlocked = kv.Value.rateLimitBlocked,
            })
            .OrderByDescending(r =>
                metricName switch
                {
                    GatewayMetricNames.RequestsTotal => r.TotalRequests,
                    GatewayMetricNames.ErrorsTotal => r.Errors,
                    GatewayMetricNames.RequestDuration => r.AverageResponseTimeMs,
                    _ => r.TotalRequests,
                }
            )
            .Take(limit)
            .ToList();

        return Task.FromResult<IReadOnlyList<RouteMetrics>>(result);
    }

    /// <inheritdoc />
    public Task<RouteMetrics?> GetRouteMetricsAsync(
        string routeId,
        CancellationToken cancellationToken = default
    )
    {
        var now = DateTimeOffset.UtcNow;
        var start = now.AddHours(-1);
        var bucketSize = TimeSpan.FromHours(1);
        var tags = new Dictionary<string, object?> { [GatewayMetricTags.RouteId] = routeId };

        var requestAggregations = _store.GetAggregations(
            GatewayMetricNames.RequestsTotal,
            start,
            now,
            bucketSize,
            tags
        );
        var errorAggregations = _store.GetAggregations(
            GatewayMetricNames.ErrorsTotal,
            start,
            now,
            bucketSize,
            tags
        );
        var durationAggregations = _store.GetAggregations(
            GatewayMetricNames.RequestDuration,
            start,
            now,
            bucketSize,
            tags
        );
        var cacheHitAggregations = _store.GetAggregations(
            GatewayMetricNames.CacheHits,
            start,
            now,
            bucketSize,
            tags
        );
        var cacheMissAggregations = _store.GetAggregations(
            GatewayMetricNames.CacheMisses,
            start,
            now,
            bucketSize,
            tags
        );
        var rateLimitBlockedAggregations = _store.GetAggregations(
            GatewayMetricNames.RateLimitBlocked,
            start,
            now,
            bucketSize,
            tags
        );

        var totalRequests = requestAggregations.Sum(a => (long)a.Sum);
        if (totalRequests == 0)
        {
            return Task.FromResult<RouteMetrics?>(null);
        }

        var totalErrors = errorAggregations.Sum(a => (long)a.Sum);
        var cacheHits = cacheHitAggregations.Sum(a => (long)a.Sum);
        var cacheMisses = cacheMissAggregations.Sum(a => (long)a.Sum);
        var cacheTotal = cacheHits + cacheMisses;
        var rateLimitBlocked = rateLimitBlockedAggregations.Sum(a => (long)a.Sum);

        var result = new RouteMetrics
        {
            RouteId = routeId,
            TotalRequests = totalRequests,
            Errors = totalErrors,
            ErrorRate = totalRequests > 0 ? (double)totalErrors / totalRequests : 0,
            AverageResponseTimeMs =
                durationAggregations.Count > 0 ? durationAggregations.Average(a => a.Average) : 0,
            P95ResponseTimeMs = durationAggregations.Count > 0 ? durationAggregations.Average(a => a.P95) : 0,
            CacheHitRate = cacheTotal > 0 ? (double)cacheHits / cacheTotal : 0,
            RateLimitBlocked = rateLimitBlocked,
        };

        return Task.FromResult<RouteMetrics?>(result);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<MetricTimeSeries>> QueryAsync(
        MetricsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var results = new List<MetricTimeSeries>();

        var metricNames = string.IsNullOrEmpty(query.MetricName)
            ? _store.GetMetricNames()
            : [query.MetricName];

        var endTime = query.EndTime ?? DateTimeOffset.UtcNow;
        var startTime = query.StartTime ?? endTime.AddHours(-1);
        var bucketSize = TimeSpan.FromSeconds(query.BucketSizeSeconds ?? 60);

        IReadOnlyDictionary<string, object?>? tagsDict = null;
        if (query.Tags != null)
        {
            var dict = query.Tags.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);
            if (!string.IsNullOrEmpty(query.RouteId))
            {
                dict[GatewayMetricTags.RouteId] = query.RouteId;
            }
            tagsDict = dict;
        }
        else if (!string.IsNullOrEmpty(query.RouteId))
        {
            tagsDict = new Dictionary<string, object?> { [GatewayMetricTags.RouteId] = query.RouteId };
        }

        foreach (var metricName in metricNames)
        {
            var aggregations = _store.GetAggregations(metricName, startTime, endTime, bucketSize, tagsDict);

            if (aggregations.Count == 0)
                continue;

            var dataPoints = aggregations
                .Select(a => new TimeSeriesDataPoint { Timestamp = a.StartTime, Value = a.Sum })
                .OrderBy(p => p.Timestamp)
                .ToList();

            if (query.Limit.HasValue && dataPoints.Count > query.Limit.Value)
            {
                dataPoints = dataPoints.TakeLast(query.Limit.Value).ToList();
            }

            results.Add(
                new MetricTimeSeries
                {
                    MetricName = metricName,
                    Tags = tagsDict ?? new Dictionary<string, object?>(),
                    DataPoints = dataPoints,
                    Aggregation = query.Aggregation,
                }
            );
        }

        return Task.FromResult<IReadOnlyList<MetricTimeSeries>>(results);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetMetricNamesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.GetMetricNames());
    }
}
