using JZVerse.MicroHuaxia.Saga.Abstractions.Models;

namespace JZVerse.MicroHuaxia.Saga.Abstractions;

/// <summary>
/// Saga 通信适配器接口（用于远程步骤执行）
/// </summary>
public interface ISagaCommunicationAdapter
{
    /// <summary>
    /// 执行远程步骤（正向操作）
    /// </summary>
    /// <param name="serviceName">目标服务名称</param>
    /// <param name="request">步骤执行请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<StepExecutionResult> ExecuteStepAsync(
        string serviceName, 
        StepExecutionRequest request, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 执行远程补偿
    /// </summary>
    /// <param name="serviceName">目标服务名称</param>
    /// <param name="request">补偿请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<CompensationResult> CompensateStepAsync(
        string serviceName, 
        CompensationRequest request, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 步骤执行请求
/// </summary>
public sealed record StepExecutionRequest
{
    /// <summary>
    /// Saga 实例 ID
    /// </summary>
    public required string SagaInstanceId { get; init; }
    
    /// <summary>
    /// 步骤 ID
    /// </summary>
    public required string StepId { get; init; }
    
    /// <summary>
    /// 步骤名称
    /// </summary>
    public required string StepName { get; init; }
    
    /// <summary>
    /// 上下文数据
    /// </summary>
    public SagaContext Context { get; init; } = new();
    
    /// <summary>
    /// 超时时间
    /// </summary>
    public TimeSpan? Timeout { get; init; }
    
    /// <summary>
    /// 请求时间戳
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// 补偿请求
/// </summary>
public sealed record CompensationRequest
{
    /// <summary>
    /// Saga 实例 ID
    /// </summary>
    public required string SagaInstanceId { get; init; }
    
    /// <summary>
    /// 步骤 ID
    /// </summary>
    public required string StepId { get; init; }
    
    /// <summary>
    /// 步骤名称
    /// </summary>
    public required string StepName { get; init; }
    
    /// <summary>
    /// 上下文数据
    /// </summary>
    public SagaContext Context { get; init; } = new();
    
    /// <summary>
    /// 原始执行结果（补偿时可能需要）
    /// </summary>
    public object? OriginalResult { get; init; }
    
    /// <summary>
    /// 请求时间戳
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
