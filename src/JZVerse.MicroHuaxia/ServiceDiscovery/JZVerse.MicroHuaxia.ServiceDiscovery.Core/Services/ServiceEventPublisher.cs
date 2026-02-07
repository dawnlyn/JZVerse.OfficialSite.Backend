using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Core.Services;

/// <summary>
/// 服务事件发布器实现
/// </summary>
public class ServiceEventPublisher(ILogger<ServiceEventPublisher> _logger) : IServiceEventPublisher
{
    private readonly List<IServiceEventListener> _listeners = [];
    private readonly Lock _lock = new();

    /// <inheritdoc />
    public async Task PublishAsync(ServiceEvent @event, CancellationToken cancellationToken = default)
    {
        IServiceEventListener[] listeners;
        lock (_lock)
        {
            listeners = [.. _listeners];
        }

        if (listeners.Length == 0)
        {
            return;
        }

        _logger.LogDebug(
            "Publishing event {EventType} for {ServiceName}:{InstanceId} to {ListenerCount} listeners",
            @event.EventType,
            @event.Instance.ServiceName,
            @event.Instance.InstanceId,
            listeners.Length
        );

        var tasks = listeners.Select(async listener =>
        {
            try
            {
                await listener.OnEventAsync(@event, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in event listener while processing {EventType} for {InstanceId}",
                    @event.EventType,
                    @event.Instance.InstanceId
                );
            }
        });

        await Task.WhenAll(tasks);
    }

    /// <inheritdoc />
    public void Subscribe(IServiceEventListener listener)
    {
        lock (_lock)
        {
            if (!_listeners.Contains(listener))
            {
                _listeners.Add(listener);
                _logger.LogDebug("Event listener subscribed: {ListenerType}", listener.GetType().Name);
            }
        }
    }

    /// <inheritdoc />
    public void Unsubscribe(IServiceEventListener listener)
    {
        lock (_lock)
        {
            if (_listeners.Remove(listener))
            {
                _logger.LogDebug("Event listener unsubscribed: {ListenerType}", listener.GetType().Name);
            }
        }
    }
}
