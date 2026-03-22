namespace JZVerse.MicroHuaxia.Security.Authorization;

/// <summary>
/// 授权引擎接口
/// </summary>
public interface IAuthorizationEngine
{
    /// <summary>
    /// 评估访问请求
    /// </summary>
    /// <param name="request">访问请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>访问决策</returns>
    Task<AccessDecision> EvaluateAsync(
        AccessRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量评估访问请求
    /// </summary>
    /// <param name="requests">访问请求列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>访问决策列表</returns>
    Task<IReadOnlyList<AccessDecision>> EvaluateBatchAsync(
        IReadOnlyList<AccessRequest> requests,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查是否允许访问
    /// </summary>
    /// <param name="subject">请求主体</param>
    /// <param name="resource">目标资源</param>
    /// <param name="action">操作类型</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否允许</returns>
    Task<bool> IsAllowedAsync(
        SecurityIdentity subject,
        string resource,
        string action,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查是否允许访问（带上下文）
    /// </summary>
    /// <param name="subject">请求主体</param>
    /// <param name="resource">目标资源</param>
    /// <param name="action">操作类型</param>
    /// <param name="context">上下文信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否允许</returns>
    Task<bool> IsAllowedAsync(
        SecurityIdentity subject,
        string resource,
        string action,
        IReadOnlyDictionary<string, object> context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 预热策略缓存
    /// </summary>
    /// <param name="namespace">命名空间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    Task WarmupCacheAsync(
        string? @namespace = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 清除策略缓存
    /// </summary>
    /// <param name="namespace">命名空间（为空则清除所有）</param>
    /// <returns>任务</returns>
    Task ClearCacheAsync(string? @namespace = null);
}
