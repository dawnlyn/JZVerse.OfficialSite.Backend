using JZVerse.MicroHuaxia.Saga.Abstractions.Models;

namespace JZVerse.MicroHuaxia.Saga.Core.Orchestration;

/// <summary>
/// Saga 状态机：管理状态转换规则
/// </summary>
public sealed class SagaStateMachine
{
    /// <summary>
    /// 检查 Saga 状态转换是否有效
    /// </summary>
    public bool CanTransition(SagaStatus from, SagaStatus to)
    {
        return (from, to) switch
        {
            // 从 Pending 可以转换到 Executing
            (SagaStatus.Pending, SagaStatus.Executing) => true,
            
            // 从 Executing 可以转换到 Completed、Compensating、Failed、TimedOut
            (SagaStatus.Executing, SagaStatus.Completed) => true,
            (SagaStatus.Executing, SagaStatus.Compensating) => true,
            (SagaStatus.Executing, SagaStatus.Failed) => true,
            (SagaStatus.Executing, SagaStatus.TimedOut) => true,
            (SagaStatus.Executing, SagaStatus.Cancelled) => true,
            
            // 从 Compensating 可以转换到 Compensated、Failed
            (SagaStatus.Compensating, SagaStatus.Compensated) => true,
            (SagaStatus.Compensating, SagaStatus.Failed) => true,
            
            // 其他转换无效
            _ => false
        };
    }

    /// <summary>
    /// 检查步骤状态转换是否有效
    /// </summary>
    public bool CanTransitionStep(StepStatus from, StepStatus to)
    {
        return (from, to) switch
        {
            // 从 Pending 可以转换到 Executing、Skipped
            (StepStatus.Pending, StepStatus.Executing) => true,
            (StepStatus.Pending, StepStatus.Skipped) => true,
            
            // 从 Executing 可以转换到 Completed、Failed
            (StepStatus.Executing, StepStatus.Completed) => true,
            (StepStatus.Executing, StepStatus.Failed) => true,
            
            // 从 Failed 可以转换到 Compensating（准备补偿）或重试（回到 Executing）
            (StepStatus.Failed, StepStatus.Compensating) => true,
            (StepStatus.Failed, StepStatus.Executing) => true, // 重试
            
            // 从 Completed 可以转换到 Compensating（需要回滚时）
            (StepStatus.Completed, StepStatus.Compensating) => true,
            
            // 从 Compensating 可以转换到 Compensated、CompensationFailed
            (StepStatus.Compensating, StepStatus.Compensated) => true,
            (StepStatus.Compensating, StepStatus.CompensationFailed) => true,
            
            // 其他转换无效
            _ => false
        };
    }

    /// <summary>
    /// 判断 Saga 是否处于终态
    /// </summary>
    public bool IsFinalState(SagaStatus status)
    {
        return status is SagaStatus.Completed 
            or SagaStatus.Compensated 
            or SagaStatus.Failed 
            or SagaStatus.TimedOut
            or SagaStatus.Cancelled;
    }

    /// <summary>
    /// 判断步骤是否处于终态
    /// </summary>
    public bool IsStepFinalState(StepStatus status)
    {
        return status is StepStatus.Completed 
            or StepStatus.Compensated 
            or StepStatus.CompensationFailed
            or StepStatus.Skipped;
    }

    /// <summary>
    /// 判断 Saga 是否需要补偿
    /// </summary>
    public bool NeedsCompensation(SagaInstance instance)
    {
        // 如果有任何步骤失败，且有已完成的可补偿步骤，则需要补偿
        var hasFailed = instance.Steps.Any(s => s.Status == StepStatus.Failed);
        var hasCompletedCompensable = instance.Steps.Any(s => 
            s.Status == StepStatus.Completed && s.IsCompensable);
        
        return hasFailed && hasCompletedCompensable;
    }
}
