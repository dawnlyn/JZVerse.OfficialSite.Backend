using System.Text;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;
using JZVerse.MicroHuaxia.Gateway.Resilience.Abstractions;
using JZVerse.MicroHuaxia.ServiceCommunication.Core.Resilience;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Gateway.Resilience.Fallback;

/// <summary>
/// 静态响应降级处理器
/// </summary>
public sealed class StaticResponseFallbackHandler : IFallbackHandler
{
    private readonly ILogger<StaticResponseFallbackHandler> _logger;

    public StaticResponseFallbackHandler(ILogger<StaticResponseFallbackHandler> logger)
    {
        _logger = logger;
    }

    public string Name => "static";

    public bool CanHandle(IGatewayResilienceContext context, Exception exception)
    {
        var fallback = context.Route.Fallback;
        return fallback is { Enabled: true, Type: FallbackType.Static };
    }

    public Task<FallbackResult> HandleAsync(
        IGatewayResilienceContext context,
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        var route = context.Route;
        var staticResponse = route.Fallback?.StaticResponse ?? GetDefaultResponse(exception);

        _logger.LogInformation(
            "Returning static fallback response for route '{RouteId}': StatusCode={StatusCode}",
            route.RouteId, staticResponse.StatusCode);

        var headers = new Dictionary<string, string[]>();
        foreach (var header in staticResponse.Headers)
        {
            headers[header.Key] = [header.Value];
        }

        var result = new FallbackResult
        {
            StatusCode = staticResponse.StatusCode,
            Body = Encoding.UTF8.GetBytes(staticResponse.Body),
            ContentType = staticResponse.ContentType,
            Headers = headers,
            Source = "static"
        };

        return Task.FromResult(result);
    }

    private static FallbackStaticResponse GetDefaultResponse(Exception exception)
    {
        // 根据异常类型返回不同的默认响应
        var (statusCode, errorType, message) = exception switch
        {
            BrokenCircuitException => (503, "CircuitBreakerOpen", "服务熔断保护中，请稍后重试"),
            TimeoutException => (504, "GatewayTimeout", "请求超时"),
            HttpRequestException => (502, "BadGateway", "后端服务不可用"),
            _ => (503, "ServiceUnavailable", "服务暂时不可用")
        };

        return new FallbackStaticResponse
        {
            StatusCode = statusCode,
            Body = $"{{\"error\":\"{errorType}\",\"message\":\"{message}\"}}",
            ContentType = "application/json"
        };
    }
}
