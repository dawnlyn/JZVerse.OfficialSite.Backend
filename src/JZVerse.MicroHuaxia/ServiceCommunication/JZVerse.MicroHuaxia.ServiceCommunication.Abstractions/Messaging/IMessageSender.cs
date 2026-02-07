namespace JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.Messaging;

/// <summary>
/// 消息发送者接口
/// </summary>
public interface IMessageSender
{
    /// <summary>
    /// 发送单向消息（无响应）
    /// </summary>
    /// <typeparam name="TMessage">消息类型</typeparam>
    /// <param name="serviceName">目标服务</param>
    /// <param name="topic">主题</param>
    /// <param name="message">消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SendAsync<TMessage>(
        string serviceName,
        string topic,
        TMessage message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送请求并等待响应
    /// </summary>
    /// <typeparam name="TRequest">请求类型</typeparam>
    /// <typeparam name="TResponse">响应类型</typeparam>
    /// <param name="serviceName">目标服务</param>
    /// <param name="topic">主题</param>
    /// <param name="request">请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<TResponse> RequestAsync<TRequest, TResponse>(
        string serviceName,
        string topic,
        TRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 消息订阅者接口
/// </summary>
public interface IMessageSubscriber
{
    /// <summary>
    /// 订阅消息
    /// </summary>
    /// <typeparam name="TMessage">消息类型</typeparam>
    /// <param name="topic">主题</param>
    /// <param name="handler">处理器</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SubscribeAsync<TMessage>(
        string topic,
        Func<TMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取消订阅
    /// </summary>
    /// <param name="topic">主题</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UnsubscribeAsync(string topic, CancellationToken cancellationToken = default);
}
