namespace JZVerse.MicroHuaxia.Security.Authorization;

/// <summary>
/// 策略存储接口
/// </summary>
public interface IPolicyStore
{
    /// <summary>
    /// 获取策略
    /// </summary>
    /// <param name="policyId">策略 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>策略信息</returns>
    Task<SecurityPolicy?> GetPolicyAsync(
        string policyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据名称获取策略
    /// </summary>
    /// <param name="name">策略名称</param>
    /// <param name="namespace">命名空间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>策略信息</returns>
    Task<SecurityPolicy?> GetPolicyByNameAsync(
        string name,
        string? @namespace = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取命名空间下的所有策略
    /// </summary>
    /// <param name="namespace">命名空间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>策略列表</returns>
    Task<IReadOnlyList<SecurityPolicy>> GetPoliciesAsync(
        string? @namespace = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建策略
    /// </summary>
    /// <param name="policy">策略信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>创建后的策略</returns>
    Task<SecurityPolicy> CreatePolicyAsync(
        SecurityPolicy policy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新策略
    /// </summary>
    /// <param name="policy">策略信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的策略</returns>
    Task<SecurityPolicy> UpdatePolicyAsync(
        SecurityPolicy policy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除策略
    /// </summary>
    /// <param name="policyId">策略 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> DeletePolicyAsync(
        string policyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 启用策略
    /// </summary>
    /// <param name="policyId">策略 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> EnablePolicyAsync(
        string policyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 禁用策略
    /// </summary>
    /// <param name="policyId">策略 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> DisablePolicyAsync(
        string policyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 订阅策略变更事件
    /// </summary>
    /// <param name="handler">变更处理器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>订阅句柄</returns>
    IAsyncEnumerable<PolicyChangeEvent> WatchPolicyChangesAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 策略变更事件
/// </summary>
public sealed record PolicyChangeEvent
{
    /// <summary>
    /// 变更类型
    /// </summary>
    public PolicyChangeType ChangeType { get; init; }

    /// <summary>
    /// 策略 ID
    /// </summary>
    public string PolicyId { get; init; } = null!;

    /// <summary>
    /// 策略名称
    /// </summary>
    public string PolicyName { get; init; } = null!;

    /// <summary>
    /// 命名空间
    /// </summary>
    public string? Namespace { get; init; }

    /// <summary>
    /// 变更时间
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 变更前策略（如果是更新或删除）
    /// </summary>
    public SecurityPolicy? PreviousPolicy { get; init; }

    /// <summary>
    /// 变更后策略（如果是创建或更新）
    /// </summary>
    public SecurityPolicy? CurrentPolicy { get; init; }
}

/// <summary>
/// 策略变更类型
/// </summary>
public enum PolicyChangeType
{
    /// <summary>
    /// 创建
    /// </summary>
    Created = 0,

    /// <summary>
    /// 更新
    /// </summary>
    Updated = 1,

    /// <summary>
    /// 删除
    /// </summary>
    Deleted = 2,

    /// <summary>
    /// 启用
    /// </summary>
    Enabled = 3,

    /// <summary>
    /// 禁用
    /// </summary>
    Disabled = 4
}
