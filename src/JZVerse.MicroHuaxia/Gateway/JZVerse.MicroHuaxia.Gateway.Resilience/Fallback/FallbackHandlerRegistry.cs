using JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;
using JZVerse.MicroHuaxia.Gateway.Resilience.Abstractions;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Gateway.Resilience.Fallback;

/// <summary>
/// 降级处理器注册表实现
/// </summary>
public sealed class FallbackHandlerRegistry : IFallbackHandlerRegistry
{
    private readonly Dictionary<string, IFallbackHandler> _handlers;
    private readonly ILogger<FallbackHandlerRegistry> _logger;

    public FallbackHandlerRegistry(
        IEnumerable<IFallbackHandler> handlers,
        ILogger<FallbackHandlerRegistry> logger)
    {
        _handlers = handlers.ToDictionary(h => h.Name, h => h, StringComparer.OrdinalIgnoreCase);
        _logger = logger;

        _logger.LogDebug("Registered {Count} fallback handlers: {Names}",
            _handlers.Count, string.Join(", ", _handlers.Keys));
    }

    public IFallbackHandler? GetHandler(string name)
    {
        return _handlers.GetValueOrDefault(name);
    }

    public IEnumerable<IFallbackHandler> GetAllHandlers()
    {
        return _handlers.Values;
    }

    public IFallbackHandler? SelectHandler(IGatewayResilienceContext context, Exception exception)
    {
        var fallbackConfig = context.Route.Fallback;
        if (fallbackConfig is not { Enabled: true })
        {
            return null;
        }

        // 根据配置的降级类型选择处理器
        var handlerName = fallbackConfig.Type switch
        {
            FallbackType.Static => "static",
            FallbackType.Cache => "cache",
            FallbackType.Custom => fallbackConfig.CustomHandlerName,
            _ => null
        };

        if (string.IsNullOrEmpty(handlerName))
        {
            _logger.LogWarning(
                "No fallback handler name configured for route '{RouteId}' with type '{Type}'",
                context.Route.RouteId, fallbackConfig.Type);
            return null;
        }

        var handler = GetHandler(handlerName);
        if (handler == null)
        {
            _logger.LogWarning(
                "Fallback handler '{HandlerName}' not found for route '{RouteId}'",
                handlerName, context.Route.RouteId);
            return null;
        }

        if (!handler.CanHandle(context, exception))
        {
            _logger.LogDebug(
                "Fallback handler '{HandlerName}' cannot handle exception for route '{RouteId}'",
                handlerName, context.Route.RouteId);
            return null;
        }

        return handler;
    }
}
