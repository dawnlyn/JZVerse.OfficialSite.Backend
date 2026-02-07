using JZVerse.MicroHuaxia.Saga.Abstractions.Models;

namespace JZVerse.MicroHuaxia.Saga.Abstractions;

/// <summary>
/// Saga 持久化存储接口
/// </summary>
public interface ISagaStore
{
    /// <summary>
    /// 保存 Saga 实例
    /// </summary>
    Task SaveAsync(SagaInstance instance, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取 Saga 实例
    /// </summary>
    Task<SagaInstance?> GetAsync(string sagaInstanceId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 更新 Saga 实例
    /// </summary>
    Task UpdateAsync(SagaInstance instance, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 更新 Saga 状态
    /// </summary>
    Task UpdateStatusAsync(
        string sagaInstanceId, 
        SagaStatus status, 
        string? errorMessage = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 更新步骤状态
    /// </summary>
    Task UpdateStepStatusAsync(
        string sagaInstanceId, 
        string stepId, 
        StepStatus status, 
        object? result = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取待恢复的 Saga 实例（状态为 Executing 或 Compensating）
    /// </summary>
    Task<IReadOnlyList<SagaInstance>> GetPendingInstancesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取超时的 Saga 实例
    /// </summary>
    Task<IReadOnlyList<SagaInstance>> GetTimeoutInstancesAsync(
        TimeSpan timeout, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 查询 Saga 实例
    /// </summary>
    Task<IReadOnlyList<SagaInstance>> QueryAsync(
        string? sagaId = null,
        SagaStatus? status = null,
        int limit = 100,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 删除 Saga 实例
    /// </summary>
    Task<bool> DeleteAsync(string sagaInstanceId, CancellationToken cancellationToken = default);
}
