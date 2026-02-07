using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;

namespace JZVerse.MicroHuaxia.MessageQueue.Abstractions.Routing;

/// <summary>
/// 交换机类型
/// </summary>
public enum ExchangeType
{
    /// <summary>
    /// 直连交换机（精确匹配路由键）
    /// </summary>
    Direct = 0,

    /// <summary>
    /// 主题交换机（通配符匹配）
    /// </summary>
    Topic = 1,

    /// <summary>
    /// 广播交换机（广播到所有绑定队列）
    /// </summary>
    Fanout = 2,

    /// <summary>
    /// 头部交换机（基于消息头匹配）
    /// </summary>
    Headers = 3
}

/// <summary>
/// 交换机接口
/// </summary>
public interface IExchange
{
    /// <summary>
    /// 交换机名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 交换机类型
    /// </summary>
    ExchangeType Type { get; }

    /// <summary>
    /// 是否持久化
    /// </summary>
    bool Durable { get; }

    /// <summary>
    /// 绑定队列
    /// </summary>
    /// <param name="queueName">队列名称</param>
    /// <param name="routingKey">路由键</param>
    /// <param name="arguments">绑定参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task BindQueueAsync(
        string queueName,
        string routingKey,
        IDictionary<string, string>? arguments = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 解绑队列
    /// </summary>
    /// <param name="queueName">队列名称</param>
    /// <param name="routingKey">路由键</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UnbindQueueAsync(
        string queueName,
        string routingKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 发布消息到交换机
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="routingKey">路由键</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task PublishAsync(
        IMessage message,
        string routingKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取绑定的队列列表
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<QueueBinding>> GetBindingsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 队列绑定信息
/// </summary>
public sealed record QueueBinding
{
    /// <summary>
    /// 队列名称
    /// </summary>
    public required string QueueName { get; init; }

    /// <summary>
    /// 路由键
    /// </summary>
    public required string RoutingKey { get; init; }

    /// <summary>
    /// 绑定参数
    /// </summary>
    public IDictionary<string, string> Arguments { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// 交换机管理器接口
/// </summary>
public interface IExchangeManager
{
    /// <summary>
    /// 声明交换机
    /// </summary>
    /// <param name="name">交换机名称</param>
    /// <param name="type">交换机类型</param>
    /// <param name="durable">是否持久化</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IExchange> DeclareExchangeAsync(
        string name,
        ExchangeType type,
        bool durable = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取交换机
    /// </summary>
    /// <param name="name">交换机名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IExchange?> GetExchangeAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除交换机
    /// </summary>
    /// <param name="name">交换机名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DeleteExchangeAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有交换机
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<IExchange>> GetAllExchangesAsync(CancellationToken cancellationToken = default);
}
