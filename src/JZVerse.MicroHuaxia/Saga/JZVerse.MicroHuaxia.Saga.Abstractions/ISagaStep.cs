using JZVerse.MicroHuaxia.Saga.Abstractions.Models;

namespace JZVerse.MicroHuaxia.Saga.Abstractions;

/// <summary>
/// Saga 步骤定义接口
/// </summary>
public interface ISagaStep
{
    /// <summary>
    /// 步骤 ID
    /// </summary>
    string StepId { get; }
    
    /// <summary>
    /// 步骤名称
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 执行顺序（从 0 开始）
    /// </summary>
    int Order { get; }
    
    /// <summary>
    /// 是否可补偿（有些步骤如查询操作无需补偿）
    /// </summary>
    bool IsCompensable { get; }
    
    /// <summary>
    /// 是否幂等（支持重试）
    /// </summary>
    bool IsIdempotent { get; }
    
    /// <summary>
    /// 步骤超时时间
    /// </summary>
    TimeSpan? Timeout { get; }
    
    /// <summary>
    /// 重试策略
    /// </summary>
    RetryPolicy? RetryPolicy { get; }
    
    /// <summary>
    /// 执行正向操作
    /// </summary>
    Task<StepExecutionResult> ExecuteAsync(SagaContext context, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 执行补偿操作
    /// </summary>
    Task<CompensationResult> CompensateAsync(SagaContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Saga 步骤基类（简化实现）
/// </summary>
public abstract class SagaStepBase : ISagaStep
{
    public abstract string StepId { get; }
    public abstract string Name { get; }
    public abstract int Order { get; }
    public virtual bool IsCompensable => true;
    public virtual bool IsIdempotent => true;
    public virtual TimeSpan? Timeout => null;
    public virtual RetryPolicy? RetryPolicy => null;
    
    public abstract Task<StepExecutionResult> ExecuteAsync(SagaContext context, CancellationToken cancellationToken = default);
    
    public virtual Task<CompensationResult> CompensateAsync(SagaContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CompensationResult.Succeeded());
    }
}

/// <summary>
/// 委托式 Saga 步骤（便于快速定义）
/// </summary>
public sealed class DelegateSagaStep : ISagaStep
{
    private readonly Func<SagaContext, CancellationToken, Task<StepExecutionResult>> _execute;
    private readonly Func<SagaContext, CancellationToken, Task<CompensationResult>>? _compensate;
    
    public DelegateSagaStep(
        string stepId,
        string name,
        int order,
        Func<SagaContext, CancellationToken, Task<StepExecutionResult>> execute,
        Func<SagaContext, CancellationToken, Task<CompensationResult>>? compensate = null)
    {
        StepId = stepId;
        Name = name;
        Order = order;
        _execute = execute;
        _compensate = compensate;
        IsCompensable = compensate != null;
    }
    
    public string StepId { get; }
    public string Name { get; }
    public int Order { get; }
    public bool IsCompensable { get; }
    public bool IsIdempotent { get; init; } = true;
    public TimeSpan? Timeout { get; init; }
    public RetryPolicy? RetryPolicy { get; init; }
    
    public Task<StepExecutionResult> ExecuteAsync(SagaContext context, CancellationToken cancellationToken = default)
        => _execute(context, cancellationToken);
    
    public Task<CompensationResult> CompensateAsync(SagaContext context, CancellationToken cancellationToken = default)
        => _compensate?.Invoke(context, cancellationToken) ?? Task.FromResult(CompensationResult.Succeeded());
}
