using JZVerse.MicroHuaxia.ConfigCenter.Abstractions;
using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.ConfigCenter.Core.Services;

/// <summary>
/// 配置事件发布器实现
/// </summary>
public class ConfigEventPublisher(ILogger<ConfigEventPublisher> _logger) : IConfigEventPublisher
{
    private readonly List<IConfigEventListener> _listeners = [];
    private readonly Lock _lock = new();

    public async Task PublishAsync(ConfigChangeEvent @event, CancellationToken cancellationToken = default)
    {
        IConfigEventListener[] listeners;
        lock (_lock)
        {
            listeners = [.. _listeners];
        }

        if (listeners.Length == 0)
        {
            _logger.LogDebug("No listeners registered for config event {EventType}", @event.EventType);
            return;
        }

        _logger.LogInformation(
            "Publishing config event {EventType} for namespace {NamespaceId} to {ListenerCount} listeners",
            @event.EventType, @event.NamespaceId, listeners.Length);

        var tasks = listeners.Select(async listener =>
        {
            try
            {
                await listener.OnEventAsync(@event, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error notifying listener {ListenerType} for event {EventType}",
                    listener.GetType().Name, @event.EventType);
            }
        });

        await Task.WhenAll(tasks);
    }

    public void Subscribe(IConfigEventListener listener)
    {
        lock (_lock)
        {
            if (!_listeners.Contains(listener))
            {
                _listeners.Add(listener);
                _logger.LogDebug("Subscribed listener {ListenerType}", listener.GetType().Name);
            }
        }
    }

    public void Unsubscribe(IConfigEventListener listener)
    {
        lock (_lock)
        {
            _listeners.Remove(listener);
            _logger.LogDebug("Unsubscribed listener {ListenerType}", listener.GetType().Name);
        }
    }
}
