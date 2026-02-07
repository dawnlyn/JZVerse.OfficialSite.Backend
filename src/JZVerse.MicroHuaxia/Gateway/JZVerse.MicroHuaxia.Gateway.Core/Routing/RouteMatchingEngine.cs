using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Gateway.Core.Routing;

/// <summary>
/// 路由匹配引擎实现
/// </summary>
public sealed class RouteMatchingEngine : IRouteMatchingEngine
{
    private readonly ILogger<RouteMatchingEngine> _logger;
    private readonly ConcurrentDictionary<string, GatewayRoute> _routes = new();
    private readonly ConcurrentDictionary<string, CompiledRoute> _compiledRoutes = new();
    private volatile IReadOnlyList<CompiledRoute> _sortedRoutes = [];
    private readonly object _updateLock = new();

    public RouteMatchingEngine(ILogger<RouteMatchingEngine> logger)
    {
        _logger = logger;
    }

    public ValueTask<RouteMatchResult?> MatchAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        var request = context.Request;
        var path = request.Path.Value ?? "/";
        var method = request.Method;

        foreach (var compiledRoute in _sortedRoutes)
        {
            if (!compiledRoute.Route.Enabled)
                continue;

            // 检查 HTTP 方法
            if (compiledRoute.Route.Match.Methods.Count > 0 &&
                !compiledRoute.Route.Match.Methods.Contains(method, StringComparer.OrdinalIgnoreCase))
                continue;

            // 检查协议
            if (compiledRoute.Route.Match.Schemes.Count > 0 &&
                !compiledRoute.Route.Match.Schemes.Contains(request.Scheme, StringComparer.OrdinalIgnoreCase))
                continue;

            // 检查主机
            if (compiledRoute.Route.Match.Hosts.Count > 0 &&
                !compiledRoute.Route.Match.Hosts.Contains(request.Host.Host, StringComparer.OrdinalIgnoreCase))
                continue;

            // 路径匹配
            var match = compiledRoute.PathRegex.Match(path);
            if (!match.Success)
                continue;

            // 检查 Header
            if (!MatchHeaders(request, compiledRoute.Route.Match.Headers))
                continue;

            // 检查 Query 参数
            if (!MatchQueryParams(request, compiledRoute.Route.Match.QueryParams))
                continue;

            // 提取路径参数
            var pathParameters = new Dictionary<string, string>();
            foreach (var paramName in compiledRoute.ParameterNames)
            {
                var group = match.Groups[paramName];
                if (group.Success)
                {
                    pathParameters[paramName] = group.Value;
                }
            }

            // 计算转换后的路径
            var transformedPath = TransformPath(path, compiledRoute.Route.Destination.PathTransform, pathParameters);

            _logger.LogDebug("路由匹配成功: {RouteId} -> {Path}", compiledRoute.Route.RouteId, path);

            return ValueTask.FromResult<RouteMatchResult?>(new RouteMatchResult
            {
                Route = compiledRoute.Route,
                PathParameters = pathParameters,
                MatchedPattern = compiledRoute.Route.Match.Path,
                TransformedPath = transformedPath
            });
        }

        _logger.LogDebug("未找到匹配的路由: {Path}", path);
        return ValueTask.FromResult<RouteMatchResult?>(null);
    }

    public void AddRoute(GatewayRoute route)
    {
        lock (_updateLock)
        {
            _routes[route.RouteId] = route;
            var compiled = CompileRoute(route);
            _compiledRoutes[route.RouteId] = compiled;
            RebuildSortedRoutes();
        }

        _logger.LogInformation("添加路由: {RouteId} - {Path}", route.RouteId, route.Match.Path);
    }

    public bool RemoveRoute(string routeId)
    {
        lock (_updateLock)
        {
            if (_routes.TryRemove(routeId, out _))
            {
                _compiledRoutes.TryRemove(routeId, out _);
                RebuildSortedRoutes();
                _logger.LogInformation("移除路由: {RouteId}", routeId);
                return true;
            }
        }

        return false;
    }

    public void UpdateRoutes(IEnumerable<GatewayRoute> routes)
    {
        lock (_updateLock)
        {
            _routes.Clear();
            _compiledRoutes.Clear();

            foreach (var route in routes)
            {
                _routes[route.RouteId] = route;
                _compiledRoutes[route.RouteId] = CompileRoute(route);
            }

            RebuildSortedRoutes();
        }

        _logger.LogInformation("批量更新路由，共 {Count} 条", _routes.Count);
    }

    public IReadOnlyList<GatewayRoute> GetAllRoutes()
    {
        return _routes.Values.ToList();
    }

    public GatewayRoute? GetRoute(string routeId)
    {
        return _routes.TryGetValue(routeId, out var route) ? route : null;
    }

    private void RebuildSortedRoutes()
    {
        // 按优先级排序，优先级相同时按路径特异性排序
        _sortedRoutes = _compiledRoutes.Values
            .OrderBy(r => r.Route.Priority)
            .ThenByDescending(r => r.Specificity)
            .ToList();
    }

    private CompiledRoute CompileRoute(GatewayRoute route)
    {
        var pathPattern = route.Match.Path;
        var parameterNames = new List<string>();

        // 转换路径模式为正则表达式
        // {param} -> (?<param>[^/]+)
        // {**catch-all} -> (?<catch_all>.*)
        var regexPattern = "^" + Regex.Replace(pathPattern, @"\{(\*\*)?([^}]+)\}", match =>
        {
            var isCatchAll = !string.IsNullOrEmpty(match.Groups[1].Value);
            var paramName = match.Groups[2].Value.Replace("-", "_");
            parameterNames.Add(paramName);

            return isCatchAll
                ? $"(?<{paramName}>.*)"
                : $"(?<{paramName}>[^/]+)";
        }) + "$";

        var regex = new Regex(regexPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // 计算路径特异性（用于排序）
        // 精确路径 > 参数路径 > 通配符
        var specificity = pathPattern.Length;
        if (pathPattern.Contains("{**"))
            specificity -= 1000;
        else if (pathPattern.Contains("{"))
            specificity -= 100;

        return new CompiledRoute
        {
            Route = route,
            PathRegex = regex,
            ParameterNames = parameterNames,
            Specificity = specificity
        };
    }

    private static bool MatchHeaders(HttpRequest request, Dictionary<string, string> requiredHeaders)
    {
        foreach (var (key, expectedValue) in requiredHeaders)
        {
            if (!request.Headers.TryGetValue(key, out var actualValue))
                return false;

            // 支持通配符匹配
            if (expectedValue != "*" && !string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static bool MatchQueryParams(HttpRequest request, Dictionary<string, string> requiredParams)
    {
        foreach (var (key, expectedValue) in requiredParams)
        {
            if (!request.Query.TryGetValue(key, out var actualValue))
                return false;

            if (expectedValue != "*" && !string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static string? TransformPath(string originalPath, string? pathTransform, Dictionary<string, string> pathParameters)
    {
        if (string.IsNullOrEmpty(pathTransform))
            return originalPath;

        var result = pathTransform;

        // 替换路径参数
        foreach (var (key, value) in pathParameters)
        {
            result = result.Replace($"{{{key}}}", value);
            result = result.Replace($"{{**{key}}}", value);
        }

        return result;
    }

    private sealed class CompiledRoute
    {
        public required GatewayRoute Route { get; init; }
        public required Regex PathRegex { get; init; }
        public required List<string> ParameterNames { get; init; }
        public int Specificity { get; init; }
    }
}
