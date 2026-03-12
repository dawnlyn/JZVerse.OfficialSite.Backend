using JZVerse.MicroHuaxia.Gateway.Abstractions;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Authentication;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;

namespace JZVerse.MicroHuaxia.Gateway.Client;

/// <summary>
/// 网关客户端接口，对应 Gateway.Server 的管理 API
/// </summary>
public interface IGatewayClient
{
    // ==================== 路由管理 ====================

    /// <summary>
    /// 获取所有路由
    /// </summary>
    Task<IReadOnlyList<GatewayRoute>> GetRoutesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定路由
    /// </summary>
    Task<GatewayRoute?> GetRouteAsync(string routeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 添加路由
    /// </summary>
    Task<GatewayRoute> AddRouteAsync(GatewayRoute route, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新路由
    /// </summary>
    Task<GatewayRoute> UpdateRouteAsync(string routeId, GatewayRoute route, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除路由
    /// </summary>
    Task<bool> DeleteRouteAsync(string routeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量更新路由
    /// </summary>
    Task<int> BatchUpdateRoutesAsync(IEnumerable<GatewayRoute> routes, CancellationToken cancellationToken = default);

    // ==================== 认证策略管理 ====================

    /// <summary>
    /// 获取所有认证策略
    /// </summary>
    Task<IReadOnlyList<AuthenticationStrategy>> GetAuthStrategiesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定认证策略
    /// </summary>
    Task<AuthenticationStrategy?> GetAuthStrategyAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// 添加认证策略
    /// </summary>
    Task<AuthenticationStrategy> AddAuthStrategyAsync(AuthenticationStrategy strategy, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除认证策略
    /// </summary>
    Task<bool> DeleteAuthStrategyAsync(string name, CancellationToken cancellationToken = default);

    // ==================== 审计日志 ====================

    /// <summary>
    /// 查询审计日志
    /// </summary>
    Task<IReadOnlyList<AuditLogEntry>> QueryAuditLogsAsync(AuditLogQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取审计日志统计
    /// </summary>
    Task<AuditLogStatistics> GetAuditStatisticsAsync(DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken cancellationToken = default);

    // ==================== 健康检查 ====================

    /// <summary>
    /// 网关健康检查
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}
