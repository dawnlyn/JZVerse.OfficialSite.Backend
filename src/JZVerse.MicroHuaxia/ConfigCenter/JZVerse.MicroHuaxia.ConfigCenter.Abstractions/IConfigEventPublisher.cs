using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ConfigCenter.Abstractions;

/// <summary>
/// 配置事件监听器接口
/// </summary>
public interface IConfigEventListener
{
    /// <summary>
    /// 处理配置事件
    /// </summary>
    Task OnEventAsync(ConfigChangeEvent @event, CancellationToken cancellationToken = default);
}

/// <summary>
/// 配置事件发布器接口
/// </summary>
public interface IConfigEventPublisher
{
    /// <summary>
    /// 发布配置变更事件
    /// </summary>
    Task PublishAsync(ConfigChangeEvent @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// 订阅事件
    /// </summary>
    void Subscribe(IConfigEventListener listener);

    /// <summary>
    /// 取消订阅
    /// </summary>
    void Unsubscribe(IConfigEventListener listener);
}
