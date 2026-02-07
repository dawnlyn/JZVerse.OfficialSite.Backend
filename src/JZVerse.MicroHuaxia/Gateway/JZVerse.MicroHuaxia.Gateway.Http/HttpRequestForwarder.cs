using System.Diagnostics;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Caching;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Configuration;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;
using JZVerse.MicroHuaxia.Gateway.Resilience.Abstractions;
using JZVerse.MicroHuaxia.Gateway.Resilience.Fallback;
using JZVerse.MicroHuaxia.Gateway.Resilience.Pipeline;
using JZVerse.MicroHuaxia.Gateway.Tracing;
using JZVerse.MicroHuaxia.Gateway.Tracing.Propagation;
using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.LoadBalancing;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Gateway.Http;

/// <summary>
/// HTTP 请求转发器实现（带弹性能力）
/// </summary>
public sealed class HttpRequestForwarder : IRequestForwarder
{
    private readonly ILogger<HttpRequestForwarder> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceDiscovery? _serviceDiscovery;
    private readonly IServiceInstanceSelector? _instanceSelector;
    private readonly IGatewayCache _cache;
    private readonly GatewayOptions _options;
    private readonly IGatewayResiliencePipelineFactory? _resiliencePipelineFactory;
    private readonly CachedResponseFallbackHandler? _cachedFallbackHandler;
    private readonly ITracePropagator? _tracePropagator;

    public HttpRequestForwarder(
        ILogger<HttpRequestForwarder> logger,
        IHttpClientFactory httpClientFactory,
        IGatewayCache cache,
        IOptions<GatewayOptions> options,
        IServiceDiscovery? serviceDiscovery = null,
        IServiceInstanceSelector? instanceSelector = null,
        IGatewayResiliencePipelineFactory? resiliencePipelineFactory = null,
        CachedResponseFallbackHandler? cachedFallbackHandler = null,
        ITracePropagator? tracePropagator = null)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _serviceDiscovery = serviceDiscovery;
        _instanceSelector = instanceSelector;
        _cache = cache;
        _options = options.Value;
        _resiliencePipelineFactory = resiliencePipelineFactory;
        _cachedFallbackHandler = cachedFallbackHandler;
        _tracePropagator = tracePropagator;
    }

    public async Task ForwardAsync(HttpContext context, RouteMatchResult matchResult, CancellationToken cancellationToken = default)
    {
        var route = matchResult.Route;

        // 创建弹性上下文
        var resilienceContext = new GatewayResilienceContext
        {
            Route = route,
            HttpContext = context,
            MatchResult = matchResult
        };

        // 如果配置了弹性管道，使用弹性管道执行
        if (_resiliencePipelineFactory != null && HasResilienceConfig(route))
        {
            await ForwardWithResilienceAsync(resilienceContext, cancellationToken);
        }
        else
        {
            // 降级到原有的简单转发逻辑
            await ForwardSimpleAsync(context, matchResult, cancellationToken);
        }

        // 将弹性上下文信息写入 HttpContext.Items（供审计日志使用）
        resilienceContext.WriteToHttpContext();
    }

    private bool HasResilienceConfig(GatewayRoute route)
    {
        return route.Retry?.Enabled == true ||
               route.CircuitBreaker?.Enabled == true ||
               route.Bulkhead?.Enabled == true ||
               route.Fallback?.Enabled == true;
    }

    private async Task ForwardWithResilienceAsync(
        GatewayResilienceContext resilienceContext,
        CancellationToken cancellationToken)
    {
        var context = resilienceContext.HttpContext;
        var route = resilienceContext.Route;
        var matchResult = resilienceContext.MatchResult;

        var pipeline = _resiliencePipelineFactory!.GetOrCreate(route);

        try
        {
            await pipeline.ExecuteAsync(resilienceContext, async (ctx, ct) =>
            {
                // 解析目标地址
                var targetAddress = await ResolveTargetAddressAsync(route.Destination, context, ct);
                if (string.IsNullOrEmpty(targetAddress))
                {
                    throw new InvalidOperationException(
                        $"无法解析目标地址: ServiceName={route.Destination.ServiceName}, DirectAddress={route.Destination.DirectAddress}");
                }

                ctx.TargetAddress = targetAddress;
                context.Items["TargetInstance"] = targetAddress;

                // 构建目标 URI
                var targetPath = matchResult.TransformedPath ?? context.Request.Path.Value ?? "/";
                var targetUri = new Uri(new Uri(targetAddress), targetPath + context.Request.QueryString);

                _logger.LogDebug("转发请求: {Method} {OriginalPath} -> {TargetUri}",
                    context.Request.Method, context.Request.Path, targetUri);

                // 创建请求消息
                var requestMessage = CreateRequestMessage(context, targetUri, _tracePropagator);

                // 发送请求
                var client = _httpClientFactory.CreateClient("GatewayForwarder");
                var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, ct);

                // 检查是否需要重试（基于状态码）
                if (route.Retry?.Enabled == true &&
                    route.Retry.RetryableStatusCodes.Contains((int)response.StatusCode))
                {
                    throw new HttpRequestException(
                        $"Retryable status code: {response.StatusCode}",
                        null,
                        response.StatusCode);
                }

                // 写入响应
                await WriteResponseAsync(context, response, matchResult, ct);

                // 保存降级缓存（如果配置了缓存降级）
                if (_cachedFallbackHandler != null && route.Fallback?.Type == FallbackType.Cache)
                {
                    var cachedResponse = new CachedResponse
                    {
                        StatusCode = context.Response.StatusCode,
                        Headers = context.Response.Headers.ToDictionary(h => h.Key, h => h.Value.ToArray()),
                        Body = await GetResponseBodyAsync(response, ct),
                        ContentType = response.Content.Headers.ContentType?.ToString()
                    };
                    await _cachedFallbackHandler.SaveFallbackCacheAsync(ctx, cachedResponse, ct);
                }

                return true;
            }, cancellationToken);
        }
        catch (FallbackExecutedException ex)
        {
            // 降级处理成功，写入降级响应
            await WriteFallbackResponseAsync(context, ex.Result, cancellationToken);
        }
        catch (BulkheadRejectedException)
        {
            // 舱壁拒绝但没有降级处理器，返回 503
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "ServiceUnavailable",
                message = "服务繁忙，请稍后重试"
            }, cancellationToken);
        }
    }

    private async Task ForwardSimpleAsync(
        HttpContext context,
        RouteMatchResult matchResult,
        CancellationToken cancellationToken)
    {
        var route = matchResult.Route;
        var destination = route.Destination;

        // 解析目标地址
        var targetAddress = await ResolveTargetAddressAsync(destination, context, cancellationToken);
        if (string.IsNullOrEmpty(targetAddress))
        {
            _logger.LogError("无法解析目标地址: ServiceName={ServiceName}, DirectAddress={DirectAddress}",
                destination.ServiceName, destination.DirectAddress);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new { error = "ServiceUnavailable", message = "无法找到可用的服务实例" });
            return;
        }

        context.Items["TargetInstance"] = targetAddress;

        // 构建目标 URI
        var targetPath = matchResult.TransformedPath ?? context.Request.Path.Value ?? "/";
        var targetUri = new Uri(new Uri(targetAddress), targetPath + context.Request.QueryString);

        _logger.LogDebug("转发请求: {Method} {OriginalPath} -> {TargetUri}",
            context.Request.Method, context.Request.Path, targetUri);

        // 创建请求消息
        var requestMessage = CreateRequestMessage(context, targetUri, _tracePropagator);

        // 设置超时
        var timeout = route.Timeout ?? _options.DefaultTimeout;
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var client = _httpClientFactory.CreateClient("GatewayForwarder");
            var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token);

            // 写入响应
            await WriteResponseAsync(context, response, matchResult, cancellationToken);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _logger.LogWarning("请求超时: {TargetUri}", targetUri);
            context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
            await context.Response.WriteAsJsonAsync(new { error = "GatewayTimeout", message = "请求超时" });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "转发请求失败: {TargetUri}", targetUri);
            context.Items["ErrorMessage"] = ex.Message;
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            await context.Response.WriteAsJsonAsync(new { error = "BadGateway", message = "后端服务不可用" });
        }
    }

    private async Task WriteFallbackResponseAsync(
        HttpContext context,
        FallbackResult fallbackResult,
        CancellationToken cancellationToken)
    {
        context.Response.StatusCode = fallbackResult.StatusCode;

        if (fallbackResult.ContentType != null)
        {
            context.Response.ContentType = fallbackResult.ContentType;
        }

        foreach (var header in fallbackResult.Headers)
        {
            context.Response.Headers[header.Key] = header.Value;
        }

        // 添加降级标记头
        context.Response.Headers["X-Gateway-Fallback"] = "true";
        context.Response.Headers["X-Gateway-Fallback-Source"] = fallbackResult.Source ?? "unknown";

        await context.Response.Body.WriteAsync(fallbackResult.Body, cancellationToken);
    }

    private async Task<byte[]> GetResponseBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private async Task<string?> ResolveTargetAddressAsync(
        RouteDestination destination,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        // 优先使用直接地址
        if (!string.IsNullOrEmpty(destination.DirectAddress))
        {
            return destination.DirectAddress;
        }

        // 使用服务发现
        if (!string.IsNullOrEmpty(destination.ServiceName))
        {
            if (_instanceSelector is not null)
            {
                var selectionContext = new LoadBalancerContext
                {
                    PreferredVersion = destination.PreferredVersion,
                    PreferredTags = destination.PreferredTags.Count > 0 ? destination.PreferredTags : null
                };

                var instance = await _instanceSelector.SelectAsync(destination.ServiceName, selectionContext, cancellationToken);
                return instance?.Address;
            }

            if (_serviceDiscovery is not null)
            {
                var instances = await _serviceDiscovery.GetInstancesAsync(destination.ServiceName, cancellationToken);
                var healthyInstances = instances
                    .Where(i => i.Enabled && (destination.HealthCheckRequired ? i.Health == ServiceDiscovery.Abstractions.Models.HealthStatus.Healthy : true))
                    .ToList();

                if (healthyInstances.Count > 0)
                {
                    // 简单轮询
                    var index = Random.Shared.Next(healthyInstances.Count);
                    return healthyInstances[index].Address;
                }
            }
        }

        return null;
    }

    private static HttpRequestMessage CreateRequestMessage(HttpContext context, Uri targetUri, ITracePropagator? tracePropagator = null)
    {
        var request = context.Request;
        var requestMessage = new HttpRequestMessage
        {
            Method = new HttpMethod(request.Method),
            RequestUri = targetUri
        };

        // 复制请求头
        foreach (var header in request.Headers)
        {
            // 跳过某些不应该转发的头
            if (IsHopByHopHeader(header.Key))
                continue;

            if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
            {
                requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        // 添加 X-Forwarded 头
        var clientIp = GetClientIp(context);
        requestMessage.Headers.TryAddWithoutValidation("X-Forwarded-For", clientIp);
        requestMessage.Headers.TryAddWithoutValidation("X-Forwarded-Proto", request.Scheme);
        requestMessage.Headers.TryAddWithoutValidation("X-Forwarded-Host", request.Host.ToString());
        requestMessage.Headers.TryAddWithoutValidation("X-Gateway-Request-Id", context.TraceIdentifier);

        // 注入追踪上下文
        if (tracePropagator != null && Activity.Current != null)
        {
            tracePropagator.Inject(Activity.Current, requestMessage);
        }

        // 复制请求体
        if (request.ContentLength > 0 || request.Headers.ContainsKey("Transfer-Encoding"))
        {
            requestMessage.Content = new StreamContent(request.Body);

            if (request.ContentType is not null)
            {
                requestMessage.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(request.ContentType);
            }

            if (request.ContentLength.HasValue)
            {
                requestMessage.Content.Headers.ContentLength = request.ContentLength;
            }
        }

        return requestMessage;
    }

    private async Task WriteResponseAsync(
        HttpContext context,
        HttpResponseMessage response,
        RouteMatchResult matchResult,
        CancellationToken cancellationToken)
    {
        // 设置状态码
        context.Response.StatusCode = (int)response.StatusCode;

        // 复制响应头
        foreach (var header in response.Headers)
        {
            if (IsHopByHopHeader(header.Key))
                continue;

            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        foreach (var header in response.Content.Headers)
        {
            if (IsHopByHopHeader(header.Key))
                continue;

            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        // 添加网关响应头
        context.Response.Headers["X-Gateway-Time"] = DateTimeOffset.UtcNow.ToString("O");

        // 读取响应体
        var responseBody = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        // 缓存响应（如果配置了缓存）
        var cacheConfig = matchResult.Route.Cache;
        if (cacheConfig is not null && cacheConfig.Enabled &&
            HttpMethods.IsGet(context.Request.Method) &&
            cacheConfig.CacheableStatusCodes.Contains(context.Response.StatusCode))
        {
            if (context.Items.TryGetValue("CacheKey", out var cacheKeyObj) && cacheKeyObj is string cacheKey)
            {
                var cachedResponse = new CachedResponse
                {
                    StatusCode = context.Response.StatusCode,
                    Headers = context.Response.Headers.ToDictionary(h => h.Key, h => h.Value.ToArray()),
                    Body = responseBody,
                    ContentType = response.Content.Headers.ContentType?.ToString(),
                    ETag = response.Headers.ETag?.Tag,
                    LastModified = response.Content.Headers.LastModified
                };

                await _cache.SetAsync(cacheKey, cachedResponse, cacheConfig.Ttl, cancellationToken);
            }
        }

        // 写入响应体
        await context.Response.Body.WriteAsync(responseBody, cancellationToken);
    }

    private static bool IsHopByHopHeader(string headerName)
    {
        return headerName.Equals("Connection", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Keep-Alive", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Proxy-Authenticate", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("TE", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Trailer", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Upgrade", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetClientIp(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor;
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }
}
