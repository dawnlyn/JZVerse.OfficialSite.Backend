using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.Saga.Abstractions;
using JZVerse.MicroHuaxia.Saga.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Saga.Core.Storage;

/// <summary>
/// 内存 Saga 存储实现
/// </summary>
public sealed class MemorySagaStore : ISagaStore, IDisposable
{
    private readonly ILogger<MemorySagaStore> _logger;
    private readonly ConcurrentDictionary<string, SagaInstance> _instances = new();
    private readonly Timer _cleanupTimer;
    private bool _disposed;
    
    public MemorySagaStore(ILogger<MemorySagaStore> logger)
    {
        _logger = logger;
        
        // 每小时清理已完成超过24小时的实例
        _cleanupTimer = new Timer(
            CleanupCallback,
            null,
            TimeSpan.FromHours(1),
            TimeSpan.FromHours(1));
    }

    public Task SaveAsync(SagaInstance instance, CancellationToken cancellationToken = default)
    {
        if (_disposed) return Task.CompletedTask;
        
        if (!_instances.TryAdd(instance.InstanceId, instance))
        {
            throw new InvalidOperationException($"Saga instance {instance.InstanceId} already exists");
        }
        
        _logger.LogDebug("Saved saga instance {InstanceId} for saga {SagaId}", 
            instance.InstanceId, instance.SagaId);
        
        return Task.CompletedTask;
    }

    public Task<SagaInstance?> GetAsync(string sagaInstanceId, CancellationToken cancellationToken = default)
    {
        _instances.TryGetValue(sagaInstanceId, out var instance);
        return Task.FromResult(instance);
    }

    public Task UpdateAsync(SagaInstance instance, CancellationToken cancellationToken = default)
    {
        if (_disposed) return Task.CompletedTask;
        
        _instances[instance.InstanceId] = instance;
        
        _logger.LogDebug("Updated saga instance {InstanceId}, status: {Status}", 
            instance.InstanceId, instance.Status);
        
        return Task.CompletedTask;
    }

    public Task UpdateStatusAsync(
        string sagaInstanceId, 
        SagaStatus status, 
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        if (_instances.TryGetValue(sagaInstanceId, out var instance))
        {
            instance.Status = status;
            instance.ErrorMessage = errorMessage;
            instance.Version++;
            
            if (status is SagaStatus.Completed or SagaStatus.Compensated or SagaStatus.Failed)
            {
                instance.CompletedAt = DateTimeOffset.UtcNow;
            }
            
            _logger.LogDebug("Updated saga {InstanceId} status to {Status}", sagaInstanceId, status);
        }
        
        return Task.CompletedTask;
    }

    public Task UpdateStepStatusAsync(
        string sagaInstanceId, 
        string stepId, 
        StepStatus status, 
        object? result = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        if (_instances.TryGetValue(sagaInstanceId, out var instance))
        {
            var step = instance.Steps.FirstOrDefault(s => s.StepId == stepId);
            if (step != null)
            {
                step.Status = status;
                step.ErrorMessage = errorMessage;
                
                if (status == StepStatus.Completed)
                {
                    step.ExecutionResult = result;
                    step.CompletedAt = DateTimeOffset.UtcNow;
                }
                else if (status == StepStatus.Compensated)
                {
                    step.CompensationResult = result;
                    step.CompletedAt = DateTimeOffset.UtcNow;
                }
                
                _logger.LogDebug("Updated step {StepId} in saga {InstanceId} to {Status}", 
                    stepId, sagaInstanceId, status);
            }
        }
        
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SagaInstance>> GetPendingInstancesAsync(CancellationToken cancellationToken = default)
    {
        var pending = _instances.Values
            .Where(i => i.Status is SagaStatus.Executing or SagaStatus.Compensating)
            .ToList();
        
        return Task.FromResult<IReadOnlyList<SagaInstance>>(pending);
    }

    public Task<IReadOnlyList<SagaInstance>> GetTimeoutInstancesAsync(
        TimeSpan timeout, 
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow - timeout;
        var timedOut = _instances.Values
            .Where(i => i.Status is SagaStatus.Executing or SagaStatus.Compensating)
            .Where(i => i.StartedAt.HasValue && i.StartedAt.Value < cutoff)
            .ToList();
        
        return Task.FromResult<IReadOnlyList<SagaInstance>>(timedOut);
    }

    public Task<IReadOnlyList<SagaInstance>> QueryAsync(
        string? sagaId = null,
        SagaStatus? status = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var query = _instances.Values.AsEnumerable();
        
        if (!string.IsNullOrEmpty(sagaId))
        {
            query = query.Where(i => i.SagaId == sagaId);
        }
        
        if (status.HasValue)
        {
            query = query.Where(i => i.Status == status.Value);
        }
        
        var result = query
            .OrderByDescending(i => i.CreatedAt)
            .Take(limit)
            .ToList();
        
        return Task.FromResult<IReadOnlyList<SagaInstance>>(result);
    }

    public Task<bool> DeleteAsync(string sagaInstanceId, CancellationToken cancellationToken = default)
    {
        var removed = _instances.TryRemove(sagaInstanceId, out _);
        return Task.FromResult(removed);
    }

    private void CleanupCallback(object? state)
    {
        if (_disposed) return;
        
        try
        {
            var cutoff = DateTimeOffset.UtcNow.AddHours(-24);
            var toRemove = _instances.Values
                .Where(i => i.Status is SagaStatus.Completed or SagaStatus.Compensated or SagaStatus.Failed)
                .Where(i => i.CompletedAt.HasValue && i.CompletedAt.Value < cutoff)
                .Select(i => i.InstanceId)
                .ToList();
            
            foreach (var id in toRemove)
            {
                _instances.TryRemove(id, out _);
            }
            
            if (toRemove.Count > 0)
            {
                _logger.LogInformation("Cleaned up {Count} completed saga instances", toRemove.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during saga cleanup");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cleanupTimer.Dispose();
        _instances.Clear();
    }
}
