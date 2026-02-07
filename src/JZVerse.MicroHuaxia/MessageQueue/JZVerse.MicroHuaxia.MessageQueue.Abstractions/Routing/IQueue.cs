namespace JZVerse.MicroHuaxia.MessageQueue.Abstractions.Routing;

/// <summary>
/// 队列接口
/// </summary>
public interface IQueue
{
    /// <summary>
    /// 队列名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 是否持久化
    /// </summary>
    bool Durable { get; }

    /// <summary>
    /// 是否排他队列
    /// </summary>
    bool Exclusive { get; }

    /// <summary>
    /// 是否自动删除
    /// </summary>
    bool AutoDelete { get; }

    /// <summary>
    /// 队列中的消息数量
    /// </summary>
    Task<long> GetMessageCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 队列中的消费者数量
    /// </summary>
    Task<int> GetConsumerCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 清空队列
    /// </summary>
    Task PurgeAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 队列管理器接口
/// </summary>
public interface IQueueManager
{
    /// <summary>
    /// 声明队列
    /// </summary>
    /// <param name="name">队列名称</param>
    /// <param name="durable">是否持久化</param>
    /// <param name="exclusive">是否排他</param>
    /// <param name="autoDelete">是否自动删除</param>
    /// <param name="arguments">队列参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IQueue> DeclareQueueAsync(
        string name,
        bool durable = true,
        bool exclusive = false,
        bool autoDelete = false,
        IDictionary<string, object>? arguments = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取队列
    /// </summary>
    /// <param name="name">队列名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IQueue?> GetQueueAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除队列
    /// </summary>
    /// <param name="name">队列名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DeleteQueueAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有队列
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<IQueue>> GetAllQueuesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 队列参数常量
/// </summary>
public static class QueueArguments
{
    /// <summary>
    /// 消息过期时间（毫秒）
    /// </summary>
    public const string MessageTtl = "x-message-ttl";

    /// <summary>
    /// 队列最大长度
    /// </summary>
    public const string MaxLength = "x-max-length";

    /// <summary>
    /// 队列最大字节数
    /// </summary>
    public const string MaxLengthBytes = "x-max-length-bytes";

    /// <summary>
    /// 溢出行为
    /// </summary>
    public const string OverflowBehavior = "x-overflow";

    /// <summary>
    /// 死信交换机
    /// </summary>
    public const string DeadLetterExchange = "x-dead-letter-exchange";

    /// <summary>
    /// 死信路由键
    /// </summary>
    public const string DeadLetterRoutingKey = "x-dead-letter-routing-key";

    /// <summary>
    /// 最大优先级
    /// </summary>
    public const string MaxPriority = "x-max-priority";
}
