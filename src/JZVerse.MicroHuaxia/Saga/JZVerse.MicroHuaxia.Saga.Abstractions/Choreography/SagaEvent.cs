namespace JZVerse.MicroHuaxia.Saga.Abstractions.Choreography;

/// <summary>
/// Saga 事件基类
/// </summary>
public abstract record SagaEvent
{
    /// <summary>
    /// 事件 ID
    /// </summary>
    public string EventId { get; init; } = Guid.NewGuid().ToString("N");
    
    /// <summary>
    /// Saga 实例 ID（关联同一事务的所有事件）
    /// </summary>
    public required string SagaInstanceId { get; init; }
    
    /// <summary>
    /// 事件类型名称
    /// </summary>
    public string EventType => GetType().Name;
    
    /// <summary>
    /// 事件时间戳
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// 关联 ID（用于追踪链路）
    /// </summary>
    public string? CorrelationId { get; init; }
    
    /// <summary>
    /// 是否为补偿事件
    /// </summary>
    public bool IsCompensation { get; init; }
}

/// <summary>
/// Saga 命令事件（请求执行某操作）
/// </summary>
public abstract record SagaCommandEvent : SagaEvent
{
    /// <summary>
    /// 源服务名称
    /// </summary>
    public string? SourceService { get; init; }
    
    /// <summary>
    /// 目标服务名称
    /// </summary>
    public required string TargetService { get; init; }
}

/// <summary>
/// Saga 完成事件（通知操作完成）
/// </summary>
public record SagaCompletedEvent : SagaEvent
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// 错误消息
    /// </summary>
    public string? Error { get; init; }
    
    /// <summary>
    /// 结果数据
    /// </summary>
    public object? Result { get; init; }
}

/// <summary>
/// Saga 失败事件（触发补偿）
/// </summary>
public record SagaFailedEvent : SagaEvent
{
    /// <summary>
    /// 失败的步骤
    /// </summary>
    public required string FailedStep { get; init; }
    
    /// <summary>
    /// 错误消息
    /// </summary>
    public required string Error { get; init; }
    
    /// <summary>
    /// 需要补偿的步骤列表
    /// </summary>
    public IReadOnlyList<string> StepsToCompensate { get; init; } = [];
}
