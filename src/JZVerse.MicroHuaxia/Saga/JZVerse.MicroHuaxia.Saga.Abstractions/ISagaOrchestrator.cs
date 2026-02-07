using JZVerse.MicroHuaxia.Saga.Abstractions.Models;

namespace JZVerse.MicroHuaxia.Saga.Abstractions;

/// <summary>
/// Saga 协调器接口
/// </summary>
public interface ISagaOrchestrator
{
    /// <summary>
    /// 启动一个新的 Saga 实例
    /// </summary>
    /// <param name="sagaDefinition">Saga 定义</param>
    /// <param name="initialContext">初始上下文数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Saga 实例 ID</returns>
    Task<string> StartAsync(
        ISagaDefinition sagaDefinition, 
        SagaContext? initialContext = null, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 恢复一个已存在的 Saga 实例（用于故障恢复）
    /// </summary>
    /// <param name="sagaInstanceId">Saga 实例 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功恢复</returns>
    Task<bool> ResumeAsync(string sagaInstanceId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 手动触发 Saga 补偿
    /// </summary>
    /// <param name="sagaInstanceId">Saga 实例 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task CompensateAsync(string sagaInstanceId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 查询 Saga 实例状态
    /// </summary>
    /// <param name="sagaInstanceId">Saga 实例 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Saga 实例或 null</returns>
    Task<SagaInstance?> GetInstanceAsync(string sagaInstanceId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 取消 Saga（触发补偿）
    /// </summary>
    /// <param name="sagaInstanceId">Saga 实例 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功取消</returns>
    Task<bool> CancelAsync(string sagaInstanceId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 查询多个 Saga 实例
    /// </summary>
    /// <param name="sagaId">Saga 定义 ID（可选）</param>
    /// <param name="status">状态过滤（可选）</param>
    /// <param name="limit">返回数量限制</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<SagaInstance>> QueryInstancesAsync(
        string? sagaId = null,
        SagaStatus? status = null,
        int limit = 100,
        CancellationToken cancellationToken = default);
}
