namespace JZVerse.MicroHuaxia.Saga.Abstractions.Choreography;

/// <summary>
/// Saga 事件发布器接口
/// </summary>
public interface ISagaEventPublisher
{
    /// <summary>
    /// 发布 Saga 事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="event">事件</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : SagaEvent;
    
    /// <summary>
    /// 发布命令事件到指定服务
    /// </summary>
    /// <typeparam name="TCommand">命令类型</typeparam>
    /// <param name="command">命令</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SendCommandAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : SagaCommandEvent;
}

/// <summary>
/// Saga 事件处理器接口
/// </summary>
/// <typeparam name="TEvent">事件类型</typeparam>
public interface ISagaEventHandler<in TEvent> where TEvent : SagaEvent
{
    /// <summary>
    /// 处理事件
    /// </summary>
    /// <param name="event">事件</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}

/// <summary>
/// Saga 参与者接口（编排式模式下的服务角色）
/// </summary>
public interface ISagaParticipant
{
    /// <summary>
    /// 参与者名称（服务名）
    /// </summary>
    string ParticipantName { get; }
    
    /// <summary>
    /// 订阅的事件类型
    /// </summary>
    IReadOnlyList<Type> SubscribedEventTypes { get; }
}

/// <summary>
/// Saga 参与者基类
/// </summary>
/// <typeparam name="TCommandEvent">命令事件类型</typeparam>
/// <typeparam name="TCompletedEvent">完成事件类型</typeparam>
public abstract class SagaParticipantBase<TCommandEvent, TCompletedEvent> : ISagaParticipant, ISagaEventHandler<TCommandEvent>
    where TCommandEvent : SagaCommandEvent
    where TCompletedEvent : SagaCompletedEvent, new()
{
    protected readonly ISagaEventPublisher EventPublisher;
    
    protected SagaParticipantBase(ISagaEventPublisher eventPublisher)
    {
        EventPublisher = eventPublisher;
    }
    
    public abstract string ParticipantName { get; }
    
    public virtual IReadOnlyList<Type> SubscribedEventTypes => [typeof(TCommandEvent), typeof(SagaFailedEvent)];
    
    public async Task HandleAsync(TCommandEvent @event, CancellationToken cancellationToken = default)
    {
        if (@event.IsCompensation)
        {
            await HandleCompensationAsync(@event, cancellationToken);
        }
        else
        {
            await HandleCommandAsync(@event, cancellationToken);
        }
    }
    
    /// <summary>
    /// 处理命令（正向操作）
    /// </summary>
    protected abstract Task HandleCommandAsync(TCommandEvent command, CancellationToken cancellationToken);
    
    /// <summary>
    /// 处理补偿（反向操作）
    /// </summary>
    protected abstract Task HandleCompensationAsync(TCommandEvent command, CancellationToken cancellationToken);
    
    /// <summary>
    /// 发布完成事件
    /// </summary>
    protected async Task PublishCompletedAsync(
        string sagaInstanceId, 
        bool success, 
        object? result = null, 
        string? error = null,
        CancellationToken cancellationToken = default)
    {
        var completedEvent = new TCompletedEvent
        {
            SagaInstanceId = sagaInstanceId,
            Success = success,
            Result = result,
            Error = error
        };
        
        await EventPublisher.PublishAsync(completedEvent, cancellationToken);
    }
}
