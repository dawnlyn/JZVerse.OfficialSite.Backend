using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.Saga.Abstractions.Choreography;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Saga.Core.Choreography;

/// <summary>
/// 内存事件总线（用于进程内编排式 Saga）
/// </summary>
public sealed class InMemorySagaEventBus : ISagaEventPublisher, IDisposable
{
    private readonly ILogger<InMemorySagaEventBus> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<Type, List<Func<SagaEvent, CancellationToken, Task>>> _handlers = new();
    private bool _disposed;

    public InMemorySagaEventBus(
        ILogger<InMemorySagaEventBus> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// 注册事件处理器
    /// </summary>
    public void Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) where TEvent : SagaEvent
    {
        var eventType = typeof(TEvent);
        var handlers = _handlers.GetOrAdd(eventType, _ => new List<Func<SagaEvent, CancellationToken, Task>>());
        
        handlers.Add((e, ct) => handler((TEvent)e, ct));
        
        _logger.LogDebug("Subscribed handler for event type {EventType}", eventType.Name);
    }

    /// <summary>
    /// 注册类型化处理器
    /// </summary>
    public void Subscribe<TEvent, THandler>() 
        where TEvent : SagaEvent 
        where THandler : ISagaEventHandler<TEvent>
    {
        Subscribe<TEvent>(async (@event, ct) =>
        {
            var handler = (THandler?)_serviceProvider.GetService(typeof(THandler));
            if (handler != null)
            {
                await handler.HandleAsync(@event, ct);
            }
            else
            {
                _logger.LogWarning("No handler found for event type {EventType}", typeof(TEvent).Name);
            }
        });
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) 
        where TEvent : SagaEvent
    {
        if (_disposed) return;
        
        var eventType = typeof(TEvent);
        
        _logger.LogDebug("Publishing event {EventType} for saga {SagaInstanceId}", 
            eventType.Name, @event.SagaInstanceId);
        
        if (_handlers.TryGetValue(eventType, out var handlers))
        {
            foreach (var handler in handlers.ToList())
            {
                try
                {
                    await handler(@event, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling event {EventType}", eventType.Name);
                }
            }
        }
        else
        {
            _logger.LogWarning("No handlers registered for event type {EventType}", eventType.Name);
        }
    }

    public Task SendCommandAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default) 
        where TCommand : SagaCommandEvent
    {
        _logger.LogDebug("Sending command {CommandType} to service {TargetService}", 
            typeof(TCommand).Name, command.TargetService);
        
        return PublishAsync(command, cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _handlers.Clear();
    }
}
