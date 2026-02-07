using JZVerse.MicroHuaxia.Saga.Abstractions;
using JZVerse.MicroHuaxia.Saga.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Saga.Core.Compensation;

/// <summary>
/// 补偿引擎：负责执行 Saga 步骤的反向补偿操作
/// </summary>
public sealed class CompensationEngine
{
    private readonly ILogger<CompensationEngine> _logger;
    private readonly ISagaStore _sagaStore;

    public CompensationEngine(
        ILogger<CompensationEngine> logger,
        ISagaStore sagaStore)
    {
        _logger = logger;
        _sagaStore = sagaStore;
    }

    /// <summary>
    /// 执行补偿：按照步骤的反向顺序执行补偿操作
    /// </summary>
    /// <param name="instance">Saga 实例</param>
    /// <param name="definition">Saga 定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>补偿是否全部成功</returns>
    public async Task<bool> CompensateAsync(
        SagaInstance instance,
        ISagaDefinition definition,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting compensation for saga {InstanceId}", instance.InstanceId);
        
        // 更新状态为补偿中
        instance.Status = SagaStatus.Compensating;
        await _sagaStore.UpdateStatusAsync(instance.InstanceId, SagaStatus.Compensating, cancellationToken: cancellationToken);
        
        // 获取需要补偿的步骤：已完成且可补偿的步骤，按 Order 倒序
        var stepsToCompensate = instance.Steps
            .Where(s => s.Status == StepStatus.Completed && s.IsCompensable)
            .OrderByDescending(s => s.Order)
            .ToList();
        
        _logger.LogDebug("Found {Count} steps to compensate for saga {InstanceId}", 
            stepsToCompensate.Count, instance.InstanceId);
        
        var allSuccess = true;
        
        foreach (var stepInstance in stepsToCompensate)
        {
            var stepDef = definition.Steps.FirstOrDefault(s => s.StepId == stepInstance.StepId);
            if (stepDef == null)
            {
                _logger.LogWarning("Step definition not found for step {StepId}", stepInstance.StepId);
                continue;
            }
            
            var success = await CompensateStepAsync(instance, stepInstance, stepDef, cancellationToken);
            if (!success)
            {
                allSuccess = false;
                
                // 根据策略决定是否继续补偿其他步骤
                if (instance.Strategy == CompensationStrategy.Backward)
                {
                    // 继续尝试补偿其他步骤
                    _logger.LogWarning("Step {StepId} compensation failed, continuing with other steps", 
                        stepInstance.StepId);
                }
            }
        }
        
        // 更新最终状态
        if (allSuccess)
        {
            instance.Status = SagaStatus.Compensated;
            instance.CompletedAt = DateTimeOffset.UtcNow;
            await _sagaStore.UpdateStatusAsync(instance.InstanceId, SagaStatus.Compensated, cancellationToken: cancellationToken);
            _logger.LogInformation("Saga {InstanceId} compensation completed successfully", instance.InstanceId);
        }
        else
        {
            instance.Status = SagaStatus.Failed;
            instance.ErrorMessage = "One or more compensation steps failed";
            await _sagaStore.UpdateStatusAsync(instance.InstanceId, SagaStatus.Failed, "One or more compensation steps failed", cancellationToken);
            _logger.LogError("Saga {InstanceId} compensation failed", instance.InstanceId);
        }
        
        return allSuccess;
    }

    private async Task<bool> CompensateStepAsync(
        SagaInstance instance,
        SagaStepInstance stepInstance,
        ISagaStep stepDef,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Compensating step {StepId} ({StepName}) for saga {InstanceId}", 
            stepInstance.StepId, stepInstance.Name, instance.InstanceId);
        
        // 更新步骤状态
        stepInstance.Status = StepStatus.Compensating;
        stepInstance.StartedAt = DateTimeOffset.UtcNow;
        await _sagaStore.UpdateStepStatusAsync(
            instance.InstanceId, 
            stepInstance.StepId, 
            StepStatus.Compensating,
            cancellationToken: cancellationToken);
        
        var retryPolicy = stepDef.RetryPolicy ?? new RetryPolicy();
        var maxRetries = retryPolicy.MaxRetries;
        
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                // 执行补偿操作
                var result = await stepDef.CompensateAsync(instance.Context, cancellationToken);
                
                if (result.Success)
                {
                    stepInstance.Status = StepStatus.Compensated;
                    stepInstance.CompletedAt = DateTimeOffset.UtcNow;
                    await _sagaStore.UpdateStepStatusAsync(
                        instance.InstanceId,
                        stepInstance.StepId,
                        StepStatus.Compensated,
                        cancellationToken: cancellationToken);
                    
                    _logger.LogDebug("Step {StepId} compensated successfully", stepInstance.StepId);
                    return true;
                }
                
                // 补偿失败
                if (!result.Retryable || attempt >= maxRetries)
                {
                    stepInstance.Status = StepStatus.CompensationFailed;
                    stepInstance.ErrorMessage = result.Error;
                    await _sagaStore.UpdateStepStatusAsync(
                        instance.InstanceId,
                        stepInstance.StepId,
                        StepStatus.CompensationFailed,
                        errorMessage: result.Error,
                        cancellationToken: cancellationToken);
                    
                    _logger.LogError("Step {StepId} compensation failed: {Error}", 
                        stepInstance.StepId, result.Error);
                    return false;
                }
                
                // 等待重试
                var delay = retryPolicy.GetDelay(attempt);
                _logger.LogWarning("Step {StepId} compensation failed, retrying in {Delay}ms (attempt {Attempt}/{MaxRetries})", 
                    stepInstance.StepId, delay.TotalMilliseconds, attempt + 1, maxRetries);
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during step {StepId} compensation", stepInstance.StepId);
                
                if (attempt >= maxRetries)
                {
                    stepInstance.Status = StepStatus.CompensationFailed;
                    stepInstance.ErrorMessage = ex.Message;
                    await _sagaStore.UpdateStepStatusAsync(
                        instance.InstanceId,
                        stepInstance.StepId,
                        StepStatus.CompensationFailed,
                        errorMessage: ex.Message,
                        cancellationToken: cancellationToken);
                    return false;
                }
                
                var delay = retryPolicy.GetDelay(attempt);
                await Task.Delay(delay, cancellationToken);
            }
        }
        
        return false;
    }
}
