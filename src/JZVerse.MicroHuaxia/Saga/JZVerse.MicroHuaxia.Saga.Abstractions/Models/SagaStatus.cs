namespace JZVerse.MicroHuaxia.Saga.Abstractions.Models;

/// <summary>
/// Saga 状态枚举
/// </summary>
public enum SagaStatus
{
    /// <summary>
    /// 待执行（刚创建）
    /// </summary>
    Pending = 0,
    
    /// <summary>
    /// 执行中（正向执行步骤）
    /// </summary>
    Executing = 1,
    
    /// <summary>
    /// 补偿中（反向执行补偿操作）
    /// </summary>
    Compensating = 2,
    
    /// <summary>
    /// 已完成（所有步骤成功）
    /// </summary>
    Completed = 3,
    
    /// <summary>
    /// 已补偿（全部补偿完成）
    /// </summary>
    Compensated = 4,
    
    /// <summary>
    /// 失败（无法恢复）
    /// </summary>
    Failed = 5,
    
    /// <summary>
    /// 已超时
    /// </summary>
    TimedOut = 6,
    
    /// <summary>
    /// 已取消
    /// </summary>
    Cancelled = 7
}

/// <summary>
/// 步骤状态枚举
/// </summary>
public enum StepStatus
{
    /// <summary>
    /// 待执行
    /// </summary>
    Pending = 0,
    
    /// <summary>
    /// 执行中
    /// </summary>
    Executing = 1,
    
    /// <summary>
    /// 已完成
    /// </summary>
    Completed = 2,
    
    /// <summary>
    /// 执行失败
    /// </summary>
    Failed = 3,
    
    /// <summary>
    /// 补偿中
    /// </summary>
    Compensating = 4,
    
    /// <summary>
    /// 已补偿
    /// </summary>
    Compensated = 5,
    
    /// <summary>
    /// 补偿失败
    /// </summary>
    CompensationFailed = 6,
    
    /// <summary>
    /// 跳过（不需要补偿）
    /// </summary>
    Skipped = 7
}

/// <summary>
/// 补偿策略
/// </summary>
public enum CompensationStrategy
{
    /// <summary>
    /// 向后补偿：失败时反向执行所有已完成步骤的补偿操作
    /// </summary>
    Backward,
    
    /// <summary>
    /// 向前重试：失败时尝试重试当前步骤，直到成功或达到最大重试次数
    /// </summary>
    ForwardRecovery,
    
    /// <summary>
    /// 混合模式：先重试，重试失败后再补偿
    /// </summary>
    Hybrid
}
