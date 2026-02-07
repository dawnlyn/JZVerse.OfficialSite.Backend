using System.Threading.Channels;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.MessageQueue.Storage.Memory;

/// <summary>
/// 基于 Channel 的高性能内存队列
/// </summary>
public sealed class MemoryQueue : IAsyncDisposable
{
    private readonly ILogger<MemoryQueue> _logger;
    private readonly string _name;
    private readonly Channel<IMessageEnvelope> _channel;
    private readonly MemoryQueueOptions _options;
    private long _enqueuedCount;
    private long _dequeuedCount;

    public string Name => _name;
    public long EnqueuedCount => Interlocked.Read(ref _enqueuedCount);
    public long DequeuedCount => Interlocked.Read(ref _dequeuedCount);
    public int PendingCount => _channel.Reader.Count;

    public MemoryQueue(string name, ILogger<MemoryQueue> logger, MemoryQueueOptions? options = null)
    {
        _name = name;
        _logger = logger;
        _options = options ?? new MemoryQueueOptions();

        var channelOptions = new BoundedChannelOptions(_options.Capacity)
        {
            FullMode = _options.FullMode,
            SingleReader = _options.SingleReader,
            SingleWriter = _options.SingleWriter,
            AllowSynchronousContinuations = false
        };

        _channel = Channel.CreateBounded<IMessageEnvelope>(channelOptions);
    }

    /// <summary>
    /// 入队消息
    /// </summary>
    public async ValueTask<bool> EnqueueAsync(IMessageEnvelope envelope, CancellationToken cancellationToken = default)
    {
        try
        {
            await _channel.Writer.WriteAsync(envelope, cancellationToken);
            Interlocked.Increment(ref _enqueuedCount);
            return true;
        }
        catch (ChannelClosedException)
        {
            _logger.LogWarning("Queue {Name} is closed", _name);
            return false;
        }
    }

    /// <summary>
    /// 尝试入队（非阻塞）
    /// </summary>
    public bool TryEnqueue(IMessageEnvelope envelope)
    {
        if (_channel.Writer.TryWrite(envelope))
        {
            Interlocked.Increment(ref _enqueuedCount);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 出队消息
    /// </summary>
    public async ValueTask<IMessageEnvelope?> DequeueAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var envelope = await _channel.Reader.ReadAsync(cancellationToken);
            Interlocked.Increment(ref _dequeuedCount);
            return envelope;
        }
        catch (ChannelClosedException)
        {
            return null;
        }
    }

    /// <summary>
    /// 尝试出队（非阻塞）
    /// </summary>
    public bool TryDequeue(out IMessageEnvelope? envelope)
    {
        if (_channel.Reader.TryRead(out envelope))
        {
            Interlocked.Increment(ref _dequeuedCount);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 批量出队
    /// </summary>
    public async ValueTask<IReadOnlyList<IMessageEnvelope>> DequeueBatchAsync(
        int batchSize,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var result = new List<IMessageEnvelope>(batchSize);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            while (result.Count < batchSize && !cts.Token.IsCancellationRequested)
            {
                if (_channel.Reader.TryRead(out var envelope))
                {
                    result.Add(envelope);
                    Interlocked.Increment(ref _dequeuedCount);
                }
                else if (result.Count == 0)
                {
                    // 如果还没有消息，等待第一条
                    var firstEnvelope = await _channel.Reader.ReadAsync(cts.Token);
                    result.Add(firstEnvelope);
                    Interlocked.Increment(ref _dequeuedCount);
                }
                else
                {
                    // 已经有消息了，不再等待
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 超时或取消
        }

        return result;
    }

    /// <summary>
    /// 创建消息流
    /// </summary>
    public IAsyncEnumerable<IMessageEnvelope> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }

    /// <summary>
    /// 关闭队列
    /// </summary>
    public void Complete()
    {
        _channel.Writer.Complete();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Complete();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// 内存队列选项
/// </summary>
public sealed class MemoryQueueOptions
{
    /// <summary>
    /// 队列容量
    /// </summary>
    public int Capacity { get; set; } = 100_000;

    /// <summary>
    /// 队列满时的行为
    /// </summary>
    public BoundedChannelFullMode FullMode { get; set; } = BoundedChannelFullMode.Wait;

    /// <summary>
    /// 是否单消费者
    /// </summary>
    public bool SingleReader { get; set; } = false;

    /// <summary>
    /// 是否单生产者
    /// </summary>
    public bool SingleWriter { get; set; } = false;
}
