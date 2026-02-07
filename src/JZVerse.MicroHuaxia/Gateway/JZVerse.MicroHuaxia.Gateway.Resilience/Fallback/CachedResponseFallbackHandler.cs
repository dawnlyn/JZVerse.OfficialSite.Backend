using System.Text;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Caching;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;
using JZVerse.MicroHuaxia.Gateway.Resilience.Abstractions;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Gateway.Resilience.Fallback;

/// <summary>
/// 缓存降级处理器
/// 当服务不可用时，返回缓存的最后一次成功响应
/// </summary>
public sealed class CachedResponseFallbackHandler : IFallbackHandler
{
    private readonly IGatewayCache _cache;
    private readonly ILogger<CachedResponseFallbackHandler> _logger;

    public CachedResponseFallbackHandler(
        IGatewayCache cache,
        ILogger<CachedResponseFallbackHandler> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public string Name => "cache";

    public bool CanHandle(IGatewayResilienceContext context, Exception exception)
    {
        var fallback = context.Route.Fallback;
        return fallback is { Enabled: true, Type: FallbackType.Cache };
    }

    public async Task<FallbackResult> HandleAsync(
        IGatewayResilienceContext context,
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        var route = context.Route;
        var cacheOptions = route.Fallback?.CacheOptions;

        // 构建降级缓存键
        var fallbackCacheKey = BuildFallbackCacheKey(context);

        // 尝试从缓存获取响应
        var cachedResponse = await _cache.TryGetAsync(fallbackCacheKey, cancellationToken);

        if (cachedResponse != null)
        {
            _logger.LogInformation(
                "Returning cached fallback response for route '{RouteId}': CacheKey={CacheKey}",
                route.RouteId, fallbackCacheKey);

            return new FallbackResult
            {
                StatusCode = cachedResponse.StatusCode,
                Body = cachedResponse.Body,
                ContentType = cachedResponse.ContentType,
                Headers = cachedResponse.Headers,
                Source = "cache"
            };
        }

        // 缓存不存在，检查是否使用默认响应
        if (cacheOptions?.UseDefaultIfNotCached == true && cacheOptions.DefaultResponse != null)
        {
            _logger.LogInformation(
                "Cache miss for route '{RouteId}', returning default response: CacheKey={CacheKey}",
                route.RouteId, fallbackCacheKey);

            var defaultResponse = cacheOptions.DefaultResponse;
            var headers = new Dictionary<string, string[]>();
            foreach (var header in defaultResponse.Headers)
            {
                headers[header.Key] = [header.Value];
            }

            return new FallbackResult
            {
                StatusCode = defaultResponse.StatusCode,
                Body = Encoding.UTF8.GetBytes(defaultResponse.Body),
                ContentType = defaultResponse.ContentType,
                Headers = headers,
                Source = "cache-default"
            };
        }

        // 没有可用的降级响应
        _logger.LogWarning(
            "No cached fallback available for route '{RouteId}': CacheKey={CacheKey}",
            route.RouteId, fallbackCacheKey);

        return FallbackResult.FromStatic(503,
            "{\"error\":\"ServiceUnavailable\",\"message\":\"服务暂时不可用，且无缓存可用\"}");
    }

    /// <summary>
    /// 构建降级缓存键
    /// </summary>
    private static string BuildFallbackCacheKey(IGatewayResilienceContext context)
    {
        var httpContext = context.HttpContext;
        var route = context.Route;

        // 使用路由ID + 请求路径 + 方法作为缓存键
        var path = context.MatchResult.TransformedPath ?? httpContext.Request.Path.Value ?? "/";
        return $"fallback:{route.RouteId}:{httpContext.Request.Method}:{path}";
    }

    /// <summary>
    /// 保存成功响应到降级缓存
    /// 应在成功请求后调用
    /// </summary>
    public async Task SaveFallbackCacheAsync(
        IGatewayResilienceContext context,
        CachedResponse response,
        CancellationToken cancellationToken = default)
    {
        var cacheOptions = context.Route.Fallback?.CacheOptions;
        if (cacheOptions == null)
        {
            return;
        }

        var fallbackCacheKey = BuildFallbackCacheKey(context);
        await _cache.SetAsync(fallbackCacheKey, response, cacheOptions.MaxAge, cancellationToken);

        _logger.LogDebug(
            "Saved fallback cache for route '{RouteId}': CacheKey={CacheKey}, TTL={TTL}",
            context.Route.RouteId, fallbackCacheKey, cacheOptions.MaxAge);
    }
}
