using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;

namespace JZVerse.MicroHuaxia.Gateway.Core.Repositories;

/// <summary>
/// 内存路由存储实现
/// </summary>
public sealed class InMemoryRouteRepository : IRouteRepository
{
    private readonly ConcurrentDictionary<string, GatewayRoute> _routes = new();

    public Task<IReadOnlyList<GatewayRoute>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<GatewayRoute>>(_routes.Values.ToList());
    }

    public Task<GatewayRoute?> GetByIdAsync(string routeId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_routes.TryGetValue(routeId, out var route) ? route : null);
    }

    public Task AddAsync(GatewayRoute route, CancellationToken cancellationToken = default)
    {
        if (!_routes.TryAdd(route.RouteId, route))
        {
            throw new InvalidOperationException($"路由 {route.RouteId} 已存在");
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(GatewayRoute route, CancellationToken cancellationToken = default)
    {
        _routes[route.RouteId] = route;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string routeId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_routes.TryRemove(routeId, out _));
    }

    public Task ReplaceAllAsync(IEnumerable<GatewayRoute> routes, CancellationToken cancellationToken = default)
    {
        _routes.Clear();
        foreach (var route in routes)
        {
            _routes[route.RouteId] = route;
        }

        return Task.CompletedTask;
    }
}
