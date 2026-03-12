using JZVerse.MicroHuaxia.Dashboard.Abstractions.Models;

namespace JZVerse.MicroHuaxia.Dashboard.Abstractions.Services;

/// <summary>
/// 网关 API 客户端接口
/// </summary>
public interface IGatewayApiClient
{
    /// <summary>
    /// 获取网关统计数据
    /// </summary>
    Task<GatewayStats> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有路由
    /// </summary>
    Task<List<RouteInfo>> GetRoutesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取单个路由
    /// </summary>
    Task<RouteInfo?> GetRouteAsync(string routeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建或更新路由
    /// </summary>
    Task<RouteInfo> UpsertRouteAsync(RouteInfo route, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除路由
    /// </summary>
    Task DeleteRouteAsync(string routeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询审计日志
    /// </summary>
    Task<(List<AuditLog> Items, int Total)> QueryAuditLogsAsync(AuditLogQuery query, CancellationToken cancellationToken = default);
}
