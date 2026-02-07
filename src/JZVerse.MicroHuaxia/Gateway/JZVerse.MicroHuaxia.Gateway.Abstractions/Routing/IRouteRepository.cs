namespace JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;

/// <summary>
/// 路由存储接口
/// </summary>
public interface IRouteRepository
{
    /// <summary>
    /// 获取所有路由
    /// </summary>
    Task<IReadOnlyList<GatewayRoute>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定路由
    /// </summary>
    /// <param name="routeId">路由 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<GatewayRoute?> GetByIdAsync(string routeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 添加路由
    /// </summary>
    /// <param name="route">路由配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(GatewayRoute route, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新路由
    /// </summary>
    /// <param name="route">路由配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateAsync(GatewayRoute route, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除路由
    /// </summary>
    /// <param name="routeId">路由 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> DeleteAsync(string routeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量更新路由（替换所有路由）
    /// </summary>
    /// <param name="routes">路由配置列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ReplaceAllAsync(IEnumerable<GatewayRoute> routes, CancellationToken cancellationToken = default);
}
