using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions;

/// <summary>
/// 服务事件监听器接口
/// </summary>
public interface IServiceEventListener
{
    /// <summary>
    /// 处理服务事件
    /// </summary>
    /// <param name="event">服务事件</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task OnEventAsync(ServiceEvent @event, CancellationToken cancellationToken = default);
}

/// <summary>
/// 服务事件发布器接口
/// </summary>
public interface IServiceEventPublisher
{
    /// <summary>
    /// 发布事件
    /// </summary>
    /// <param name="event">服务事件</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task PublishAsync(ServiceEvent @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <param name="listener">事件监听器</param>
    void Subscribe(IServiceEventListener listener);

    /// <summary>
    /// 取消订阅
    /// </summary>
    /// <param name="listener">事件监听器</param>
    void Unsubscribe(IServiceEventListener listener);
}
