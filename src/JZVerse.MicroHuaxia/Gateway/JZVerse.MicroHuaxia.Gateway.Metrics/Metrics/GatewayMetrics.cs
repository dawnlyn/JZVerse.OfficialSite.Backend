using System.Diagnostics.Metrics;

namespace JZVerse.MicroHuaxia.Gateway.Metrics;

/// <summary>
/// 网关指标收集器
/// 使用 OpenTelemetry Metrics API 收集网关各环节的指标数据
/// </summary>
public sealed class GatewayMetrics
{
    private readonly Meter _meter;

    // ==================== 请求指标仪器 ====================

    private readonly Counter<long> _requestsTotal;
    private readonly Histogram<double> _requestDuration;
    private readonly Histogram<long> _requestSize;
    private readonly Histogram<long> _responseSize;

    // ==================== 认证指标仪器 ====================

    private readonly Counter<long> _authAttempts;
    private readonly Counter<long> _authFailures;
    private readonly Histogram<double> _authDuration;

    // ==================== 限流指标仪器 ====================

    private readonly Counter<long> _rateLimitAllowed;
    private readonly Counter<long> _rateLimitBlocked;

    // ==================== 缓存指标仪器 ====================

    private readonly Counter<long> _cacheHits;
    private readonly Counter<long> _cacheMisses;
    private readonly Counter<long> _cacheEvictions;

    // ==================== 转发指标仪器 ====================

    private readonly Counter<long> _forwardAttempts;
    private readonly Counter<long> _forwardFailures;
    private readonly Histogram<double> _forwardDuration;

    // ==================== 弹性指标仪器 ====================

    private readonly Counter<long> _retryAttempts;
    private readonly Counter<long> _circuitBreakerTrips;
    private readonly Counter<long> _bulkheadRejected;
    private readonly Counter<long> _fallbackExecutions;

    // ==================== 错误指标仪器 ====================

    private readonly Counter<long> _errorsTotal;
    private readonly Counter<long> _timeoutsTotal;

    // ==================== 可观察指标数据源 ====================

    private Func<int> _getActiveRequests = () => 0;
    private Func<(int size, int count)> _getCacheStats = () => (0, 0);
    private Func<IReadOnlyDictionary<string, int>> _getCircuitBreakerStates = () =>
        new Dictionary<string, int>();
    private Func<IReadOnlyDictionary<string, (int concurrent, int queue)>> _getBulkheadStats = () =>
        new Dictionary<string, (int, int)>();

    /// <summary>
    /// 创建网关指标收集器
    /// </summary>
    /// <param name="meterFactory">Meter 工厂</param>
    public GatewayMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(GatewayMetricNames.MeterName, GatewayMetricNames.Version);

        // 初始化请求指标
        _requestsTotal = _meter.CreateCounter<long>(
            GatewayMetricNames.RequestsTotal,
            "{request}",
            "网关接收的请求总数"
        );

        _requestDuration = _meter.CreateHistogram<double>(
            GatewayMetricNames.RequestDuration,
            "ms",
            "请求处理耗时"
        );

        _requestSize = _meter.CreateHistogram<long>(
            GatewayMetricNames.RequestSize,
            "By",
            "请求体大小"
        );

        _responseSize = _meter.CreateHistogram<long>(
            GatewayMetricNames.ResponseSize,
            "By",
            "响应体大小"
        );

        // 初始化认证指标
        _authAttempts = _meter.CreateCounter<long>(
            GatewayMetricNames.AuthAttempts,
            "{attempt}",
            "认证尝试总数"
        );

        _authFailures = _meter.CreateCounter<long>(
            GatewayMetricNames.AuthFailures,
            "{failure}",
            "认证失败总数"
        );

        _authDuration = _meter.CreateHistogram<double>(
            GatewayMetricNames.AuthDuration,
            "ms",
            "认证处理耗时"
        );

        // 初始化限流指标
        _rateLimitAllowed = _meter.CreateCounter<long>(
            GatewayMetricNames.RateLimitAllowed,
            "{request}",
            "限流允许通过的请求数"
        );

        _rateLimitBlocked = _meter.CreateCounter<long>(
            GatewayMetricNames.RateLimitBlocked,
            "{request}",
            "被限流拦截的请求数"
        );

        // 初始化缓存指标
        _cacheHits = _meter.CreateCounter<long>(
            GatewayMetricNames.CacheHits,
            "{hit}",
            "缓存命中次数"
        );

        _cacheMisses = _meter.CreateCounter<long>(
            GatewayMetricNames.CacheMisses,
            "{miss}",
            "缓存未命中次数"
        );

        _cacheEvictions = _meter.CreateCounter<long>(
            GatewayMetricNames.CacheEvictions,
            "{eviction}",
            "缓存驱逐次数"
        );

        // 初始化转发指标
        _forwardAttempts = _meter.CreateCounter<long>(
            GatewayMetricNames.ForwardAttempts,
            "{attempt}",
            "请求转发尝试次数"
        );

        _forwardFailures = _meter.CreateCounter<long>(
            GatewayMetricNames.ForwardFailures,
            "{failure}",
            "请求转发失败次数"
        );

        _forwardDuration = _meter.CreateHistogram<double>(
            GatewayMetricNames.ForwardDuration,
            "ms",
            "请求转发耗时"
        );

        // 初始化弹性指标
        _retryAttempts = _meter.CreateCounter<long>(
            GatewayMetricNames.RetryAttempts,
            "{attempt}",
            "重试尝试次数"
        );

        _circuitBreakerTrips = _meter.CreateCounter<long>(
            GatewayMetricNames.CircuitBreakerTrips,
            "{trip}",
            "熔断器触发次数"
        );

        _bulkheadRejected = _meter.CreateCounter<long>(
            GatewayMetricNames.BulkheadRejected,
            "{rejection}",
            "舱壁拒绝次数"
        );

        _fallbackExecutions = _meter.CreateCounter<long>(
            GatewayMetricNames.FallbackExecutions,
            "{execution}",
            "降级执行次数"
        );

        // 初始化错误指标
        _errorsTotal = _meter.CreateCounter<long>(
            GatewayMetricNames.ErrorsTotal,
            "{error}",
            "错误总数"
        );

        _timeoutsTotal = _meter.CreateCounter<long>(
            GatewayMetricNames.TimeoutsTotal,
            "{timeout}",
            "超时总数"
        );

        // 初始化可观察指标
        _meter.CreateObservableGauge(
            GatewayMetricNames.ActiveRequests,
            () => _getActiveRequests(),
            "{request}",
            "当前活跃请求数"
        );

        _meter.CreateObservableGauge(
            GatewayMetricNames.CacheSize,
            () => _getCacheStats().count,
            "{entry}",
            "缓存条目数"
        );

        _meter.CreateObservableGauge(
            GatewayMetricNames.CircuitBreakerState,
            GetCircuitBreakerMeasurements,
            "{state}",
            "熔断器状态（0=Closed, 1=Open, 2=HalfOpen）"
        );

        _meter.CreateObservableGauge(
            GatewayMetricNames.BulkheadConcurrent,
            GetBulkheadConcurrentMeasurements,
            "{connection}",
            "舱壁当前并发数"
        );

        _meter.CreateObservableGauge(
            GatewayMetricNames.BulkheadQueueLength,
            GetBulkheadQueueMeasurements,
            "{request}",
            "舱壁当前队列长度"
        );
    }

    // ==================== 状态提供者设置方法 ====================

    /// <summary>
    /// 设置活跃请求数提供者
    /// </summary>
    public void SetActiveRequestsProvider(Func<int> provider) => _getActiveRequests = provider;

    /// <summary>
    /// 设置缓存统计提供者
    /// </summary>
    public void SetCacheStatsProvider(Func<(int size, int count)> provider) => _getCacheStats = provider;

    /// <summary>
    /// 设置熔断器状态提供者
    /// </summary>
    public void SetCircuitBreakerStatsProvider(Func<IReadOnlyDictionary<string, int>> provider) =>
        _getCircuitBreakerStates = provider;

    /// <summary>
    /// 设置舱壁统计提供者
    /// </summary>
    public void SetBulkheadStatsProvider(
        Func<IReadOnlyDictionary<string, (int concurrent, int queue)>> provider
    ) => _getBulkheadStats = provider;

    // ==================== 请求指标记录方法 ====================

    /// <summary>
    /// 记录请求
    /// </summary>
    public void RecordRequest(
        string? routeId,
        string method,
        int statusCode,
        double durationMs,
        long? requestSize = null,
        long? responseSize = null
    )
    {
        var statusCodeGroup = GetStatusCodeGroup(statusCode);
        var isSuccess = statusCode < 400;

        var tags = CreateBaseTags(routeId, method, statusCode, statusCodeGroup, isSuccess);

        _requestsTotal.Add(1, tags);
        _requestDuration.Record(durationMs, tags);

        if (requestSize.HasValue)
        {
            _requestSize.Record(requestSize.Value, tags);
        }

        if (responseSize.HasValue)
        {
            _responseSize.Record(responseSize.Value, tags);
        }

        // 记录错误
        if (statusCode >= 500)
        {
            _errorsTotal.Add(
                1,
                new(GatewayMetricTags.RouteId, routeId ?? "unknown"),
                new(GatewayMetricTags.HttpStatusCode, statusCode),
                new(GatewayMetricTags.ErrorType, "server_error")
            );
        }
        else if (statusCode >= 400)
        {
            _errorsTotal.Add(
                1,
                new(GatewayMetricTags.RouteId, routeId ?? "unknown"),
                new(GatewayMetricTags.HttpStatusCode, statusCode),
                new(GatewayMetricTags.ErrorType, "client_error")
            );
        }
    }

    // ==================== 认证指标记录方法 ====================

    /// <summary>
    /// 记录认证尝试
    /// </summary>
    public void RecordAuthentication(string? routeId, string strategy, bool success, double durationMs)
    {
        var result = success ? "success" : "failure";

        _authAttempts.Add(
            1,
            new(GatewayMetricTags.RouteId, routeId ?? "unknown"),
            new(GatewayMetricTags.AuthStrategy, strategy),
            new(GatewayMetricTags.AuthResult, result)
        );

        _authDuration.Record(
            durationMs,
            new(GatewayMetricTags.RouteId, routeId ?? "unknown"),
            new(GatewayMetricTags.AuthStrategy, strategy),
            new(GatewayMetricTags.AuthResult, result)
        );

        if (!success)
        {
            _authFailures.Add(
                1,
                new(GatewayMetricTags.RouteId, routeId ?? "unknown"),
                new(GatewayMetricTags.AuthStrategy, strategy)
            );
        }
    }

    // ==================== 限流指标记录方法 ====================

    /// <summary>
    /// 记录限流检查
    /// </summary>
    public void RecordRateLimit(
        string? routeId,
        string algorithm,
        string keyStrategy,
        bool allowed,
        long? remaining = null
    )
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new(GatewayMetricTags.RouteId, routeId ?? "unknown"),
            new(GatewayMetricTags.RateLimitAlgorithm, algorithm),
            new(GatewayMetricTags.RateLimitKeyStrategy, keyStrategy),
        };

        if (allowed)
        {
            _rateLimitAllowed.Add(1, tags);
        }
        else
        {
            _rateLimitBlocked.Add(1, tags);
        }
    }

    // ==================== 缓存指标记录方法 ====================

    /// <summary>
    /// 记录缓存访问
    /// </summary>
    public void RecordCacheAccess(string? routeId, bool hit)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new(GatewayMetricTags.RouteId, routeId ?? "unknown"),
            new(GatewayMetricTags.CacheResult, hit ? "hit" : "miss"),
        };

        if (hit)
        {
            _cacheHits.Add(1, tags);
        }
        else
        {
            _cacheMisses.Add(1, tags);
        }
    }

    /// <summary>
    /// 记录缓存驱逐
    /// </summary>
    public void RecordCacheEviction(string? routeId)
    {
        _cacheEvictions.Add(1, new KeyValuePair<string, object?>(GatewayMetricTags.RouteId, routeId ?? "unknown"));
    }

    // ==================== 转发指标记录方法 ====================

    /// <summary>
    /// 记录请求转发
    /// </summary>
    public void RecordForward(
        string? routeId,
        string protocol,
        string target,
        bool success,
        double durationMs
    )
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new(GatewayMetricTags.RouteId, routeId ?? "unknown"),
            new(GatewayMetricTags.ForwardProtocol, protocol),
            new(GatewayMetricTags.ForwardTarget, target),
            new(GatewayMetricTags.ForwardResult, success ? "success" : "failure"),
        };

        _forwardAttempts.Add(1, tags);
        _forwardDuration.Record(durationMs, tags);

        if (!success)
        {
            _forwardFailures.Add(
                1,
                new(GatewayMetricTags.RouteId, routeId ?? "unknown"),
                new(GatewayMetricTags.ForwardProtocol, protocol),
                new(GatewayMetricTags.ForwardTarget, target)
            );
        }
    }

    // ==================== 弹性指标记录方法 ====================

    /// <summary>
    /// 记录重试
    /// </summary>
    public void RecordRetry(string? routeId, int attempt, string? reason = null)
    {
        _retryAttempts.Add(
            1,
            new(GatewayMetricTags.RouteId, routeId ?? "unknown"),
            new(GatewayMetricTags.RetryAttempt, attempt),
            new(GatewayMetricTags.RetryReason, reason ?? "unknown")
        );
    }

    /// <summary>
    /// 记录熔断器触发
    /// </summary>
    public void RecordCircuitBreakerTrip(string name, string state)
    {
        _circuitBreakerTrips.Add(
            1,
            new(GatewayMetricTags.CircuitBreakerName, name),
            new(GatewayMetricTags.CircuitBreakerState, state)
        );
    }

    /// <summary>
    /// 记录舱壁拒绝
    /// </summary>
    public void RecordBulkheadRejection(string name)
    {
        _bulkheadRejected.Add(1, new KeyValuePair<string, object?>(GatewayMetricTags.BulkheadName, name));
    }

    /// <summary>
    /// 记录降级执行
    /// </summary>
    public void RecordFallback(string? routeId, string type, string? reason = null)
    {
        _fallbackExecutions.Add(
            1,
            new(GatewayMetricTags.RouteId, routeId ?? "unknown"),
            new(GatewayMetricTags.FallbackType, type),
            new(GatewayMetricTags.FallbackReason, reason ?? "unknown")
        );
    }

    /// <summary>
    /// 记录超时
    /// </summary>
    public void RecordTimeout(string? routeId, string? target = null)
    {
        _timeoutsTotal.Add(
            1,
            new(GatewayMetricTags.RouteId, routeId ?? "unknown"),
            new(GatewayMetricTags.ForwardTarget, target ?? "unknown")
        );
    }

    // ==================== 辅助方法 ====================

    private static KeyValuePair<string, object?>[] CreateBaseTags(
        string? routeId,
        string method,
        int statusCode,
        string statusCodeGroup,
        bool isSuccess
    ) =>
        [
            new(GatewayMetricTags.RouteId, routeId ?? "unknown"),
            new(GatewayMetricTags.HttpMethod, method),
            new(GatewayMetricTags.HttpStatusCode, statusCode),
            new(GatewayMetricTags.StatusCodeGroup, statusCodeGroup),
            new(GatewayMetricTags.IsSuccess, isSuccess),
        ];

    private static string GetStatusCodeGroup(int statusCode) =>
        statusCode switch
        {
            >= 100 and < 200 => "1xx",
            >= 200 and < 300 => "2xx",
            >= 300 and < 400 => "3xx",
            >= 400 and < 500 => "4xx",
            >= 500 => "5xx",
            _ => "unknown",
        };

    private IEnumerable<Measurement<int>> GetCircuitBreakerMeasurements()
    {
        var states = _getCircuitBreakerStates();
        foreach (var (name, state) in states)
        {
            yield return new Measurement<int>(state, new KeyValuePair<string, object?>(GatewayMetricTags.CircuitBreakerName, name));
        }
    }

    private IEnumerable<Measurement<int>> GetBulkheadConcurrentMeasurements()
    {
        var stats = _getBulkheadStats();
        foreach (var (name, (concurrent, _)) in stats)
        {
            yield return new Measurement<int>(concurrent, new KeyValuePair<string, object?>(GatewayMetricTags.BulkheadName, name));
        }
    }

    private IEnumerable<Measurement<int>> GetBulkheadQueueMeasurements()
    {
        var stats = _getBulkheadStats();
        foreach (var (name, (_, queue)) in stats)
        {
            yield return new Measurement<int>(queue, new KeyValuePair<string, object?>(GatewayMetricTags.BulkheadName, name));
        }
    }
}
