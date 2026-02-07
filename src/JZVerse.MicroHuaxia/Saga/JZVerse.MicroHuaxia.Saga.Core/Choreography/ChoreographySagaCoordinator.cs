using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.Saga.Abstractions;
using JZVerse.MicroHuaxia.Saga.Abstractions.Choreography;
using JZVerse.MicroHuaxia.Saga.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Saga.Core.Choreography;

/// <summary>
/// 编排式 Saga 实例状态
/// </summary>
public sealed class ChoreographySagaInstance
{
    public required string InstanceId { get; init; }
    public required string SagaId { get; init; }
    public SagaStatus Status { get; set; } = SagaStatus.Pending;
    public int CurrentParticipantIndex { get; set; }
    public List<string> CompletedParticipants { get; set; } = [];
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public string? Error { get; set; }
    public Dictionary<string, object?> Context { get; set; } = new();
}

/// <summary>
/// 编排式 Saga 协调器
/// </summary>
/// <remarks>
/// 与协调式不同，编排式协调器不直接调用服务，而是：
/// 1. 监听各服务发出的完成事件
/// 2. 追踪 Saga 执行进度
/// 3. 在失败时发布补偿事件
/// </remarks>
public sealed class ChoreographySagaCoordinator : ISagaEventHandler<SagaCompletedEvent>, ISagaEventHandler<SagaFailedEvent>
{
    private readonly ILogger<ChoreographySagaCoordinator> _logger;
    private readonly ISagaEventPublisher _eventPublisher;
    private readonly ISagaStore _sagaStore;
    private readonly ConcurrentDictionary<string, IChoreographySagaDefinition> _definitions = new();
    private readonly ConcurrentDictionary<string, ChoreographySagaInstance> _instances = new();

    public ChoreographySagaCoordinator(
        ILogger<ChoreographySagaCoordinator> logger,
        ISagaEventPublisher eventPublisher,
        ISagaStore sagaStore)
    {
        _logger = logger;
        _eventPublisher = eventPublisher;
        _sagaStore = sagaStore;
    }

    /// <summary>
    /// 注册 Saga 定义
    /// </summary>
    public void RegisterDefinition(IChoreographySagaDefinition definition)
    {
        _definitions[definition.SagaId] = definition;
        _logger.LogInformation("Registered choreography saga definition: {SagaId}", definition.SagaId);
    }

    /// <summary>
    /// 启动编排式 Saga
    /// </summary>
    public async Task<string> StartAsync<TStartEvent>(
        string sagaId,
        TStartEvent startEvent,
        CancellationToken cancellationToken = default)
        where TStartEvent : SagaEvent
    {
        if (!_definitions.TryGetValue(sagaId, out var definition))
        {
            throw new InvalidOperationException($"Saga definition not found: {sagaId}");
        }

        var instanceId = GenerateInstanceId();
        
        var instance = new ChoreographySagaInstance
        {
            InstanceId = instanceId,
            SagaId = sagaId,
            Status = SagaStatus.Executing
        };
        
        _instances[instanceId] = instance;
        
        // 同时保存到持久化存储
        var sagaInstance = new SagaInstance
        {
            InstanceId = instanceId,
            SagaId = sagaId,
            Name = definition.Name,
            Status = SagaStatus.Executing
        };
        await _sagaStore.SaveAsync(sagaInstance, cancellationToken);
        
        _logger.LogInformation("Started choreography saga {SagaId} with instance {InstanceId}", 
            sagaId, instanceId);
        
        // 发布启动事件
        await _eventPublisher.PublishAsync(startEvent, cancellationToken);
        
        return instanceId;
    }

    /// <summary>
    /// 处理完成事件
    /// </summary>
    public async Task HandleAsync(SagaCompletedEvent @event, CancellationToken cancellationToken = default)
    {
        if (!_instances.TryGetValue(@event.SagaInstanceId, out var instance))
        {
            _logger.LogWarning("Saga instance not found: {InstanceId}", @event.SagaInstanceId);
            return;
        }

        if (!_definitions.TryGetValue(instance.SagaId, out var definition))
        {
            _logger.LogWarning("Saga definition not found: {SagaId}", instance.SagaId);
            return;
        }

        if (@event.Success)
        {
            // 记录完成的参与者
            var currentParticipant = definition.Participants.ElementAtOrDefault(instance.CurrentParticipantIndex);
            if (currentParticipant != null)
            {
                instance.CompletedParticipants.Add(currentParticipant.Name);
            }
            
            instance.CurrentParticipantIndex++;
            
            // 检查是否所有参与者都完成
            if (instance.CurrentParticipantIndex >= definition.Participants.Count)
            {
                instance.Status = SagaStatus.Completed;
                instance.CompletedAt = DateTimeOffset.UtcNow;
                
                await _sagaStore.UpdateStatusAsync(@event.SagaInstanceId, SagaStatus.Completed, cancellationToken: cancellationToken);
                
                _logger.LogInformation("Saga {InstanceId} completed successfully", @event.SagaInstanceId);
            }
            else
            {
                _logger.LogDebug("Saga {InstanceId} progressed to participant {Index}", 
                    @event.SagaInstanceId, instance.CurrentParticipantIndex);
            }
        }
        else
        {
            // 触发补偿
            await TriggerCompensationAsync(instance, definition, @event.Error ?? "Unknown error", cancellationToken);
        }
    }

    /// <summary>
    /// 处理失败事件
    /// </summary>
    public async Task HandleAsync(SagaFailedEvent @event, CancellationToken cancellationToken = default)
    {
        if (!_instances.TryGetValue(@event.SagaInstanceId, out var instance))
        {
            return;
        }

        if (!_definitions.TryGetValue(instance.SagaId, out var definition))
        {
            return;
        }

        await TriggerCompensationAsync(instance, definition, @event.Error, cancellationToken);
    }

    private async Task TriggerCompensationAsync(
        ChoreographySagaInstance instance,
        IChoreographySagaDefinition definition,
        string error,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning("Triggering compensation for saga {InstanceId} due to: {Error}", 
            instance.InstanceId, error);
        
        instance.Status = SagaStatus.Compensating;
        instance.Error = error;
        
        await _sagaStore.UpdateStatusAsync(instance.InstanceId, SagaStatus.Compensating, error, cancellationToken);
        
        // 发布补偿事件给已完成的参与者（反向顺序）
        var participantsToCompensate = instance.CompletedParticipants.ToList();
        participantsToCompensate.Reverse();
        
        var failedEvent = new SagaFailedEvent
        {
            SagaInstanceId = instance.InstanceId,
            FailedStep = definition.Participants.ElementAtOrDefault(instance.CurrentParticipantIndex)?.Name ?? "unknown",
            Error = error,
            StepsToCompensate = participantsToCompensate
        };
        
        await _eventPublisher.PublishAsync(failedEvent, cancellationToken);
    }

    /// <summary>
    /// 获取实例状态
    /// </summary>
    public ChoreographySagaInstance? GetInstance(string instanceId)
    {
        _instances.TryGetValue(instanceId, out var instance);
        return instance;
    }

    private static string GenerateInstanceId()
    {
        return $"CSAGA{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():D13}{Random.Shared.Next(0, 99999):D5}";
    }
}
