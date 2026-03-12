using JZVerse.MicroHuaxia.Saga.Abstractions.Models;

namespace JZVerse.MicroHuaxia.Saga.Client;

/// <summary>
/// Saga 客户端接口，对应 Saga.Server 的管理 API
/// </summary>
public interface ISagaClient
{
    /// <summary>
    /// 启动一个新的 Saga 实例
    /// </summary>
    /// <param name="sagaId">Saga 定义 ID</param>
    /// <param name="initialData">初始上下文数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>创建的 Saga 实例</returns>
    Task<SagaInstance> StartSagaAsync(
        string sagaId,
        Dictionary<string, object>? initialData = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取 Saga 实例详情
    /// </summary>
    Task<SagaInstance?> GetInstanceAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询 Saga 实例列表
    /// </summary>
    Task<IReadOnlyList<SagaInstance>> QueryInstancesAsync(
        string? sagaId = null,
        SagaStatus? status = null,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 手动触发 Saga 补偿
    /// </summary>
    Task CompensateAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取消 Saga（触发补偿）
    /// </summary>
    Task<bool> CancelAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 恢复一个暂停/失败的 Saga 实例
    /// </summary>
    Task<bool> ResumeAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 健康检查
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}
