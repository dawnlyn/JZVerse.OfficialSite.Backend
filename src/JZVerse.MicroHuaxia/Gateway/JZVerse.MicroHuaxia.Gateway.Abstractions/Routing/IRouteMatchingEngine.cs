using Microsoft.AspNetCore.Http;

namespace JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;

/// <summary>
/// 路由匹配引擎接口
/// </summary>
public interface IRouteMatchingEngine
{
    /// <summary>
    /// 匹配路由
    /// </summary>
    /// <param name="context">HTTP 上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>匹配结果，未匹配返回 null</returns>
    ValueTask<RouteMatchResult?> MatchAsync(HttpContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// 添加路由
    /// </summary>
    /// <param name="route">路由配置</param>
    void AddRoute(GatewayRoute route);

    /// <summary>
    /// 移除路由
    /// </summary>
    /// <param name="routeId">路由 ID</param>
    /// <returns>是否移除成功</returns>
    bool RemoveRoute(string routeId);

    /// <summary>
    /// 批量更新路由
    /// </summary>
    /// <param name="routes">路由配置列表</param>
    void UpdateRoutes(IEnumerable<GatewayRoute> routes);

    /// <summary>
    /// 获取所有路由
    /// </summary>
    IReadOnlyList<GatewayRoute> GetAllRoutes();

    /// <summary>
    /// 获取指定路由
    /// </summary>
    /// <param name="routeId">路由 ID</param>
    GatewayRoute? GetRoute(string routeId);
}

/// <summary>
/// 路由匹配结果
/// </summary>
public sealed record RouteMatchResult
{
    /// <summary>
    /// 匹配的路由
    /// </summary>
    public required GatewayRoute Route { get; init; }

    /// <summary>
    /// 路径参数
    /// </summary>
    public Dictionary<string, string> PathParameters { get; init; } = new();

    /// <summary>
    /// 匹配的路径模式
    /// </summary>
    public required string MatchedPattern { get; init; }

    /// <summary>
    /// 转换后的目标路径
    /// </summary>
    public string? TransformedPath { get; init; }
}
