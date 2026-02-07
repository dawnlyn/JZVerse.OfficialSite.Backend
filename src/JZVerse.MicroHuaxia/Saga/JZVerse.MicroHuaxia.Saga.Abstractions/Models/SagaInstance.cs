namespace JZVerse.MicroHuaxia.Saga.Abstractions.Models;

/// <summary>
/// Saga 运行时实例
/// </summary>
public sealed class SagaInstance
{
    /// <summary>
    /// 实例 ID
    /// </summary>
    public required string InstanceId { get; init; }
    
    /// <summary>
    /// Saga 定义 ID
    /// </summary>
    public required string SagaId { get; init; }
    
    /// <summary>
    /// Saga 名称
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// 当前状态
    /// </summary>
    public SagaStatus Status { get; set; }
    
    /// <summary>
    /// 上下文数据
    /// </summary>
    public SagaContext Context { get; set; } = new();
    
    /// <summary>
    /// 步骤实例列表
    /// </summary>
    public List<SagaStepInstance> Steps { get; set; } = [];
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// 开始执行时间
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }
    
    /// <summary>
    /// 完成时间
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }
    
    /// <summary>
    /// 超时时间
    /// </summary>
    public TimeSpan? Timeout { get; init; }
    
    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// 当前执行的步骤索引
    /// </summary>
    public int CurrentStepIndex { get; set; }
    
    /// <summary>
    /// 版本号（乐观锁）
    /// </summary>
    public int Version { get; set; }
    
    /// <summary>
    /// 补偿策略
    /// </summary>
    public CompensationStrategy Strategy { get; init; } = CompensationStrategy.Backward;
}

/// <summary>
/// 步骤运行时实例
/// </summary>
public sealed class SagaStepInstance
{
    /// <summary>
    /// 步骤 ID
    /// </summary>
    public required string StepId { get; init; }
    
    /// <summary>
    /// 步骤名称
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// 执行顺序
    /// </summary>
    public required int Order { get; init; }
    
    /// <summary>
    /// 当前状态
    /// </summary>
    public StepStatus Status { get; set; }
    
    /// <summary>
    /// 是否可补偿
    /// </summary>
    public bool IsCompensable { get; init; } = true;
    
    /// <summary>
    /// 执行结果数据
    /// </summary>
    public object? ExecutionResult { get; set; }
    
    /// <summary>
    /// 补偿结果数据
    /// </summary>
    public object? CompensationResult { get; set; }
    
    /// <summary>
    /// 开始执行时间
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }
    
    /// <summary>
    /// 完成时间
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }
    
    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; set; }
    
    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; set; }
}
