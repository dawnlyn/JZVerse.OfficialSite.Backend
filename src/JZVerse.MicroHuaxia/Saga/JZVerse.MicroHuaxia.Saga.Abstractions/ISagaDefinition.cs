using JZVerse.MicroHuaxia.Saga.Abstractions.Models;

namespace JZVerse.MicroHuaxia.Saga.Abstractions;

/// <summary>
/// Saga 定义接口
/// </summary>
public interface ISagaDefinition
{
    /// <summary>
    /// Saga 唯一标识
    /// </summary>
    string SagaId { get; }
    
    /// <summary>
    /// Saga 名称（业务语义）
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 所有步骤定义（按执行顺序）
    /// </summary>
    IReadOnlyList<ISagaStep> Steps { get; }
    
    /// <summary>
    /// 全局超时时间
    /// </summary>
    TimeSpan? Timeout { get; }
    
    /// <summary>
    /// 补偿策略
    /// </summary>
    CompensationStrategy Strategy { get; }
}

/// <summary>
/// Saga 定义基类
/// </summary>
public abstract class SagaDefinitionBase : ISagaDefinition
{
    public abstract string SagaId { get; }
    public abstract string Name { get; }
    public abstract IReadOnlyList<ISagaStep> Steps { get; }
    public virtual TimeSpan? Timeout => TimeSpan.FromMinutes(5);
    public virtual CompensationStrategy Strategy => CompensationStrategy.Backward;
}
