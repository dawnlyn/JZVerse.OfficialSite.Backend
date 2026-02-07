using JZVerse.MicroHuaxia.Saga.Abstractions;
using JZVerse.MicroHuaxia.Saga.Abstractions.Models;
using JZVerse.MicroHuaxia.Saga.Core.Compensation;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Saga.Core.Orchestration;

/// <summary>
/// Saga 协调器：编排 Saga 执行流程的核心组件
/// </summary>
public sealed class SagaOrchestrator : ISagaOrchestrator
{
    private readonly ILogger<SagaOrchestrator> _logger;
    private readonly ISagaStore _sagaStore;
    private readonly SagaStateMachine _stateMachine;
    private readonly CompensationEngine _compensationEngine;
    private readonly IServiceProvider _serviceProvider;

    public SagaOrchestrator(
        ILogger<SagaOrchestrator> logger,
        ISagaStore sagaStore,
        SagaStateMachine stateMachine,
        CompensationEngine compensationEngine,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _sagaStore = sagaStore;
        _stateMachine = stateMachine;
        _compensationEngine = compensationEngine;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> StartAsync(
        ISagaDefinition sagaDefinition,
        SagaContext? initialContext = null,
        CancellationToken cancellationToken = default)
    {
        var instanceId = GenerateInstanceId();
        
        _logger.LogInformation("Starting saga {SagaId} with instance {InstanceId}", 
            sagaDefinition.SagaId, instanceId);
        
        // 创建 Saga 实例
        var instance = new SagaInstance
        {
            InstanceId = instanceId,
            SagaId = sagaDefinition.SagaId,
            Name = sagaDefinition.Name,
            Status = SagaStatus.Pending,
            Context = initialContext ?? new SagaContext(),
            Timeout = sagaDefinition.Timeout,
            Strategy = sagaDefinition.Strategy,
            Steps = sagaDefinition.Steps.Select(s => new SagaStepInstance
            {
                StepId = s.StepId,
                Name = s.Name,
                Order = s.Order,
                Status = StepStatus.Pending,
                IsCompensable = s.IsCompensable
            }).ToList()
        };
        
        // 保存实例
        await _sagaStore.SaveAsync(instance, cancellationToken);
        
        // 开始执行
        _ = ExecuteAsync(instance, sagaDefinition, cancellationToken);
        
        return instanceId;
    }

    public async Task<bool> ResumeAsync(string sagaInstanceId, CancellationToken cancellationToken = default)
    {
        var instance = await _sagaStore.GetAsync(sagaInstanceId, cancellationToken);
        if (instance == null)
        {
            _logger.LogWarning("Cannot resume saga {InstanceId}: not found", sagaInstanceId);
            return false;
        }
        
        if (_stateMachine.IsFinalState(instance.Status))
        {
            _logger.LogWarning("Cannot resume saga {InstanceId}: already in final state {Status}", 
                sagaInstanceId, instance.Status);
            return false;
        }
        
        _logger.LogInformation("Resuming saga {InstanceId} from status {Status}", 
            sagaInstanceId, instance.Status);
        
        // 需要获取 Saga 定义来恢复执行
        // 这里简化处理，实际应该从注册的定义中查找
        _logger.LogWarning("Resume requires saga definition lookup - not fully implemented");
        return false;
    }

    public async Task CompensateAsync(string sagaInstanceId, CancellationToken cancellationToken = default)
    {
        var instance = await _sagaStore.GetAsync(sagaInstanceId, cancellationToken);
        if (instance == null)
        {
            throw new InvalidOperationException($"Saga instance {sagaInstanceId} not found");
        }
        
        _logger.LogInformation("Manual compensation triggered for saga {InstanceId}", sagaInstanceId);
        
        // 需要获取 Saga 定义来执行补偿
        _logger.LogWarning("Manual compensation requires saga definition lookup - not fully implemented");
    }

    public Task<SagaInstance?> GetInstanceAsync(string sagaInstanceId, CancellationToken cancellationToken = default)
    {
        return _sagaStore.GetAsync(sagaInstanceId, cancellationToken);
    }

    public async Task<bool> CancelAsync(string sagaInstanceId, CancellationToken cancellationToken = default)
    {
        var instance = await _sagaStore.GetAsync(sagaInstanceId, cancellationToken);
        if (instance == null)
        {
            return false;
        }
        
        if (_stateMachine.IsFinalState(instance.Status))
        {
            _logger.LogWarning("Cannot cancel saga {InstanceId}: already in final state", sagaInstanceId);
            return false;
        }
        
        _logger.LogInformation("Cancelling saga {InstanceId}", sagaInstanceId);
        
        instance.Status = SagaStatus.Cancelled;
        await _sagaStore.UpdateStatusAsync(sagaInstanceId, SagaStatus.Cancelled, "Cancelled by user", cancellationToken);
        
        return true;
    }

    public Task<IReadOnlyList<SagaInstance>> QueryInstancesAsync(
        string? sagaId = null,
        SagaStatus? status = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return _sagaStore.QueryAsync(sagaId, status, limit, cancellationToken);
    }

    private async Task ExecuteAsync(
        SagaInstance instance,
        ISagaDefinition definition,
        CancellationToken cancellationToken)
    {
        try
        {
            // 更新状态为执行中
            instance.Status = SagaStatus.Executing;
            instance.StartedAt = DateTimeOffset.UtcNow;
            await _sagaStore.UpdateStatusAsync(instance.InstanceId, SagaStatus.Executing, cancellationToken: cancellationToken);
            
            // 按顺序执行每个步骤
            var sortedSteps = definition.Steps.OrderBy(s => s.Order).ToList();
            
            for (int i = 0; i < sortedSteps.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Saga {InstanceId} cancelled during execution", instance.InstanceId);
                    break;
                }
                
                var stepDef = sortedSteps[i];
                var stepInstance = instance.Steps.First(s => s.StepId == stepDef.StepId);
                
                instance.CurrentStepIndex = i;
                
                var success = await ExecuteStepAsync(instance, stepInstance, stepDef, cancellationToken);
                
                if (!success)
                {
                    _logger.LogWarning("Step {StepId} failed in saga {InstanceId}, triggering compensation", 
                        stepDef.StepId, instance.InstanceId);
                    
                    // 步骤失败，触发补偿
                    await _compensationEngine.CompensateAsync(instance, definition, cancellationToken);
                    return;
                }
            }
            
            // 所有步骤成功完成
            instance.Status = SagaStatus.Completed;
            instance.CompletedAt = DateTimeOffset.UtcNow;
            await _sagaStore.UpdateStatusAsync(instance.InstanceId, SagaStatus.Completed, cancellationToken: cancellationToken);
            
            _logger.LogInformation("Saga {InstanceId} completed successfully", instance.InstanceId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Saga {InstanceId} was cancelled", instance.InstanceId);
            await _sagaStore.UpdateStatusAsync(instance.InstanceId, SagaStatus.Cancelled, "Operation cancelled", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing saga {InstanceId}", instance.InstanceId);
            await _sagaStore.UpdateStatusAsync(instance.InstanceId, SagaStatus.Failed, ex.Message, cancellationToken);
        }
    }

    private async Task<bool> ExecuteStepAsync(
        SagaInstance instance,
        SagaStepInstance stepInstance,
        ISagaStep stepDef,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Executing step {StepId} ({StepName}) for saga {InstanceId}", 
            stepDef.StepId, stepDef.Name, instance.InstanceId);
        
        // 更新步骤状态
        stepInstance.Status = StepStatus.Executing;
        stepInstance.StartedAt = DateTimeOffset.UtcNow;
        await _sagaStore.UpdateStepStatusAsync(
            instance.InstanceId, 
            stepInstance.StepId, 
            StepStatus.Executing,
            cancellationToken: cancellationToken);
        
        var retryPolicy = stepDef.RetryPolicy ?? new RetryPolicy();
        var maxRetries = stepDef.IsIdempotent ? retryPolicy.MaxRetries : 0;
        
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                // 创建超时取消令牌
                using var timeoutCts = stepDef.Timeout.HasValue
                    ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                    : null;
                timeoutCts?.CancelAfter(stepDef.Timeout!.Value);
                
                var ct = timeoutCts?.Token ?? cancellationToken;
                
                // 执行步骤
                var result = await stepDef.ExecuteAsync(instance.Context, ct);
                
                if (result.Success)
                {
                    stepInstance.Status = StepStatus.Completed;
                    stepInstance.ExecutionResult = result.Data;
                    stepInstance.CompletedAt = DateTimeOffset.UtcNow;
                    
                    // 将结果保存到上下文
                    instance.Context.SetStepResult(stepDef.StepId, result.Data);
                    
                    await _sagaStore.UpdateStepStatusAsync(
                        instance.InstanceId,
                        stepInstance.StepId,
                        StepStatus.Completed,
                        result.Data,
                        cancellationToken: cancellationToken);
                    
                    _logger.LogDebug("Step {StepId} completed successfully", stepDef.StepId);
                    return true;
                }
                
                // 步骤执行失败
                stepInstance.RetryCount = attempt;
                
                if (!result.Retryable || attempt >= maxRetries)
                {
                    stepInstance.Status = StepStatus.Failed;
                    stepInstance.ErrorMessage = result.Error;
                    await _sagaStore.UpdateStepStatusAsync(
                        instance.InstanceId,
                        stepInstance.StepId,
                        StepStatus.Failed,
                        errorMessage: result.Error,
                        cancellationToken: cancellationToken);
                    
                    _logger.LogWarning("Step {StepId} failed: {Error}", stepDef.StepId, result.Error);
                    return false;
                }
                
                // 等待重试
                var delay = retryPolicy.GetDelay(attempt);
                _logger.LogWarning("Step {StepId} failed, retrying in {Delay}ms (attempt {Attempt}/{MaxRetries})", 
                    stepDef.StepId, delay.TotalMilliseconds, attempt + 1, maxRetries);
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // 步骤超时
                _logger.LogWarning("Step {StepId} timed out", stepDef.StepId);
                
                if (attempt >= maxRetries)
                {
                    stepInstance.Status = StepStatus.Failed;
                    stepInstance.ErrorMessage = "Step timed out";
                    await _sagaStore.UpdateStepStatusAsync(
                        instance.InstanceId,
                        stepInstance.StepId,
                        StepStatus.Failed,
                        errorMessage: "Step timed out",
                        cancellationToken: cancellationToken);
                    return false;
                }
                
                var delay = retryPolicy.GetDelay(attempt);
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw; // 整体取消，向上传播
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during step {StepId} execution", stepDef.StepId);
                
                if (attempt >= maxRetries)
                {
                    stepInstance.Status = StepStatus.Failed;
                    stepInstance.ErrorMessage = ex.Message;
                    await _sagaStore.UpdateStepStatusAsync(
                        instance.InstanceId,
                        stepInstance.StepId,
                        StepStatus.Failed,
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

    private static string GenerateInstanceId()
    {
        return $"SAGA{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():D13}{Random.Shared.Next(0, 99999):D5}";
    }
}
