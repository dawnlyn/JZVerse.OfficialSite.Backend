namespace JZVerse.MicroHuaxia.Saga.Abstractions.Choreography;

/// <summary>
/// 编排式 Saga 定义接口
/// </summary>
public interface IChoreographySagaDefinition
{
    /// <summary>
    /// Saga 唯一标识
    /// </summary>
    string SagaId { get; }
    
    /// <summary>
    /// Saga 名称
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 参与的服务列表（按执行顺序）
    /// </summary>
    IReadOnlyList<SagaParticipantInfo> Participants { get; }
    
    /// <summary>
    /// 启动事件类型
    /// </summary>
    Type StartEventType { get; }
    
    /// <summary>
    /// 完成事件类型
    /// </summary>
    Type CompletedEventType { get; }
    
    /// <summary>
    /// 全局超时时间
    /// </summary>
    TimeSpan? Timeout { get; }
}

/// <summary>
/// Saga 参与者信息
/// </summary>
public sealed record SagaParticipantInfo
{
    /// <summary>
    /// 参与者名称（服务名）
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// 执行顺序
    /// </summary>
    public required int Order { get; init; }
    
    /// <summary>
    /// 触发该参与者的事件类型
    /// </summary>
    public required Type TriggerEventType { get; init; }
    
    /// <summary>
    /// 该参与者完成后发出的事件类型
    /// </summary>
    public required Type CompletedEventType { get; init; }
    
    /// <summary>
    /// 补偿事件类型
    /// </summary>
    public Type? CompensationEventType { get; init; }
    
    /// <summary>
    /// 是否可补偿
    /// </summary>
    public bool IsCompensable { get; init; } = true;
}

/// <summary>
/// 编排式 Saga 定义基类
/// </summary>
public abstract class ChoreographySagaDefinitionBase : IChoreographySagaDefinition
{
    public abstract string SagaId { get; }
    public abstract string Name { get; }
    public abstract IReadOnlyList<SagaParticipantInfo> Participants { get; }
    public abstract Type StartEventType { get; }
    public abstract Type CompletedEventType { get; }
    public virtual TimeSpan? Timeout => TimeSpan.FromMinutes(5);
}
