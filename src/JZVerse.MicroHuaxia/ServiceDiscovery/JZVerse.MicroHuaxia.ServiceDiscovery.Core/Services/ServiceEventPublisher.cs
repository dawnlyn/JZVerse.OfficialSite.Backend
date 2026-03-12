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
            "正在发布事件 {EventType}，服务: {ServiceName}:{InstanceId}，监听器数量: {ListenerCount}",
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
                    "事件监听器处理 {EventType} 时发生错误，实例ID: {InstanceId}",
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
                _logger.LogDebug("事件监听器已订阅: {ListenerType}", listener.GetType().Name);
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
                _logger.LogDebug("事件监听器已取消订阅: {ListenerType}", listener.GetType().Name);
            }
        }
    }
}
