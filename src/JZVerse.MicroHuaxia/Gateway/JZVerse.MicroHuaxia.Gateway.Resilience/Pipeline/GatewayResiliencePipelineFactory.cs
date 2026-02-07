using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;
using JZVerse.MicroHuaxia.Gateway.Resilience.Abstractions;
using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.Resilience;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Gateway.Resilience.Pipeline;

/// <summary>
/// Gateway 弹性管道工厂实现
/// 为每个路由创建和缓存独立的弹性管道
/// </summary>
public sealed class GatewayResiliencePipelineFactory : IGatewayResiliencePipelineFactory
{
    private readonly ConcurrentDictionary<string, IGatewayResiliencePipeline> _pipelines = new();
    private readonly IBulkheadFactory _bulkheadFactory;
    private readonly ICircuitBreakerFactory _circuitBreakerFactory;
    private readonly IFallbackHandlerRegistry _fallbackRegistry;
    private readonly ILoggerFactory _loggerFactory;

    public GatewayResiliencePipelineFactory(
        IBulkheadFactory bulkheadFactory,
        ICircuitBreakerFactory circuitBreakerFactory,
        IFallbackHandlerRegistry fallbackRegistry,
        ILoggerFactory loggerFactory)
    {
        _bulkheadFactory = bulkheadFactory;
        _circuitBreakerFactory = circuitBreakerFactory;
        _fallbackRegistry = fallbackRegistry;
        _loggerFactory = loggerFactory;
    }

    public IGatewayResiliencePipeline GetOrCreate(GatewayRoute route)
    {
        return _pipelines.GetOrAdd(route.RouteId, _ => CreatePipeline(route));
    }

    private IGatewayResiliencePipeline CreatePipeline(GatewayRoute route)
    {
        // 创建舱壁（如果配置了）
        IBulkhead? bulkhead = null;
        if (route.Bulkhead is { Enabled: true })
        {
            var bulkheadOptions = new BulkheadOptions
            {
                MaxConcurrency = route.Bulkhead.MaxConcurrency,
                MaxQueueLength = route.Bulkhead.MaxQueueLength,
                QueueTimeout = route.Bulkhead.QueueTimeout
            };
            bulkhead = _bulkheadFactory.GetOrCreate($"gateway:{route.RouteId}", bulkheadOptions);
        }

        // 创建熔断器（如果配置了）
        ICircuitBreaker? circuitBreaker = null;
        if (route.CircuitBreaker is { Enabled: true })
        {
            var circuitBreakerOptions = new CircuitBreakerOptions
            {
                FailureThreshold = route.CircuitBreaker.FailureThreshold,
                SuccessThreshold = route.CircuitBreaker.SuccessThreshold,
                SamplingDuration = route.CircuitBreaker.SamplingDuration,
                BreakDuration = route.CircuitBreaker.BreakDuration
            };
            circuitBreaker = _circuitBreakerFactory.GetOrCreate($"gateway:{route.RouteId}", circuitBreakerOptions);
        }

        var logger = _loggerFactory.CreateLogger<GatewayResiliencePipeline>();

        return new GatewayResiliencePipeline(
            route,
            bulkhead,
            circuitBreaker,
            _fallbackRegistry,
            logger);
    }

    /// <summary>
    /// 刷新指定路由的管道（当路由配置变更时调用）
    /// </summary>
    public void RefreshPipeline(string routeId)
    {
        _pipelines.TryRemove(routeId, out _);
    }

    /// <summary>
    /// 清除所有缓存的管道
    /// </summary>
    public void ClearAll()
    {
        _pipelines.Clear();
    }
}
