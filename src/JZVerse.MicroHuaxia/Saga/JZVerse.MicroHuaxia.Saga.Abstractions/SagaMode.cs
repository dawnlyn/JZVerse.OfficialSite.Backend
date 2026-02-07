namespace JZVerse.MicroHuaxia.Saga.Abstractions;

/// <summary>
/// Saga 执行模式
/// </summary>
public enum SagaMode
{
    /// <summary>
    /// 协调式：中央协调器编排所有步骤，按顺序执行
    /// 优点：流程清晰、易于追踪、补偿逻辑集中
    /// 缺点：协调器是单点、服务耦合度较高
    /// </summary>
    Orchestration,
    
    /// <summary>
    /// 编排式：各服务通过事件自主协调，无中央协调器
    /// 优点：松耦合、高可用、服务自治
    /// 缺点：流程分散、难以追踪、补偿逻辑分散
    /// </summary>
    Choreography
}
