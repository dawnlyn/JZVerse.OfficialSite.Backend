using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;

namespace JZVerse.MicroHuaxia.MessageQueue.Core.Routing;

/// <summary>
/// 路由引擎接口
/// </summary>
public interface IRoutingEngine
{
    /// <summary>
    /// 路由消息到队列
    /// </summary>
    /// <param name="queueName">队列名称</param>
    /// <param name="message">消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RouteToQueueAsync(string queueName, IMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 路由消息到主题
    /// </summary>
    /// <param name="topic">主题</param>
    /// <param name="message">消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RouteToTopicAsync(string topic, IMessage message, CancellationToken cancellationToken = default);
}
