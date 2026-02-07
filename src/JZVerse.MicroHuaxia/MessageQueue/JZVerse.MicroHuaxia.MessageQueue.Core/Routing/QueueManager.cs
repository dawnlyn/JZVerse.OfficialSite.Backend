using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Routing;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.MessageQueue.Core.Routing;

/// <summary>
/// 队列实现
/// </summary>
public sealed class Queue : IQueue
{
    private readonly ILogger _logger;
    private long _messageCount;
    private int _consumerCount;

    public string Name { get; }
    public bool Durable { get; }
    public bool Exclusive { get; }
    public bool AutoDelete { get; }
    public IDictionary<string, object> Arguments { get; }

    public Queue(
        string name,
        bool durable,
        bool exclusive,
        bool autoDelete,
        IDictionary<string, object>? arguments,
        ILogger logger)
    {
        Name = name;
        Durable = durable;
        Exclusive = exclusive;
        AutoDelete = autoDelete;
        Arguments = arguments ?? new Dictionary<string, object>();
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<long> GetMessageCountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Interlocked.Read(ref _messageCount));
    }

    /// <inheritdoc />
    public Task<int> GetConsumerCountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Interlocked.CompareExchange(ref _consumerCount, 0, 0));
    }

    /// <inheritdoc />
    public Task PurgeAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Exchange(ref _messageCount, 0);
        _logger.LogInformation("Queue {QueueName} purged", Name);
        return Task.CompletedTask;
    }

    internal void IncrementMessageCount() => Interlocked.Increment(ref _messageCount);
    internal void DecrementMessageCount() => Interlocked.Decrement(ref _messageCount);
    internal void IncrementConsumerCount() => Interlocked.Increment(ref _consumerCount);
    internal void DecrementConsumerCount() => Interlocked.Decrement(ref _consumerCount);
}

/// <summary>
/// 队列管理器实现
/// </summary>
public sealed class QueueManager : IQueueManager
{
    private readonly ILogger<QueueManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<string, Queue> _queues = new();

    public QueueManager(ILogger<QueueManager> logger, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc />
    public Task<IQueue> DeclareQueueAsync(
        string name,
        bool durable = true,
        bool exclusive = false,
        bool autoDelete = false,
        IDictionary<string, object>? arguments = null,
        CancellationToken cancellationToken = default)
    {
        var queue = _queues.GetOrAdd(name, _ =>
        {
            var newQueue = new Queue(
                name,
                durable,
                exclusive,
                autoDelete,
                arguments,
                _loggerFactory.CreateLogger<Queue>());

            _logger.LogInformation("Queue {QueueName} declared (durable={Durable}, exclusive={Exclusive}, autoDelete={AutoDelete})",
                name, durable, exclusive, autoDelete);

            return newQueue;
        });

        return Task.FromResult<IQueue>(queue);
    }

    /// <inheritdoc />
    public Task<IQueue?> GetQueueAsync(string name, CancellationToken cancellationToken = default)
    {
        _queues.TryGetValue(name, out var queue);
        return Task.FromResult<IQueue?>(queue);
    }

    /// <inheritdoc />
    public Task DeleteQueueAsync(string name, CancellationToken cancellationToken = default)
    {
        if (_queues.TryRemove(name, out _))
        {
            _logger.LogInformation("Queue {QueueName} deleted", name);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IQueue>> GetAllQueuesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<IQueue>>(_queues.Values.Cast<IQueue>().ToList());
    }

    /// <summary>
    /// 获取内部队列对象
    /// </summary>
    internal Queue? GetQueue(string name)
    {
        _queues.TryGetValue(name, out var queue);
        return queue;
    }
}
