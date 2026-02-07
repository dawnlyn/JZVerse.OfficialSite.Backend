using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Delay;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.MessageQueue.Core.Delay;

/// <summary>
/// 基于时间轮的内存延迟消息存储
/// </summary>
public sealed class MemoryDelayMessageStore : IDelayMessageStore, IDisposable
{
    private readonly ILogger<MemoryDelayMessageStore> _logger;
    private readonly HierarchicalTimeWheel _timeWheel;
    private readonly ConcurrentDictionary<string, DelayedMessageEntry> _messageIndex = new();
    private readonly ConcurrentQueue<DelayedMessageEntry> _immediateQueue = new(); // 立即到期的消息
    private readonly object _lock = new();
    private bool _disposed;

    public MemoryDelayMessageStore(
        ILogger<MemoryDelayMessageStore> logger,
        ILogger<HierarchicalTimeWheel> timeWheelLogger,
        TimeWheelOptions? options = null)
    {
        _logger = logger;
        _timeWheel = new HierarchicalTimeWheel(timeWheelLogger, options ?? new TimeWheelOptions());
    }

    /// <inheritdoc />
    public Task AddAsync(IMessage message, DateTimeOffset deliveryTime, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MemoryDelayMessageStore));
        }

        var entry = new DelayedMessageEntry
        {
            Message = message,
            DeliveryTime = deliveryTime
        };

        lock (_lock)
        {
            if (_timeWheel.Add(entry))
            {
                _messageIndex[message.MessageId] = entry;
                _logger.LogDebug("Delayed message {MessageId} scheduled for {DeliveryTime}",
                    message.MessageId, deliveryTime);
            }
            else
            {
                // 消息已到期或即将到期，加入立即队列
                _immediateQueue.Enqueue(entry);
                _messageIndex[message.MessageId] = entry;
                _logger.LogDebug("Message {MessageId} already due, queued for immediate delivery",
                    message.MessageId);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IMessage>> GetDueMessagesAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return Task.FromResult<IReadOnlyList<IMessage>>(Array.Empty<IMessage>());
        }

        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var dueMessages = new List<IMessage>(batchSize);
        
        lock (_lock)
        {
            // 1. 先处理立即队列中的消息
            while (dueMessages.Count < batchSize && _immediateQueue.TryDequeue(out var immediateEntry))
            {
                if (!immediateEntry.IsCancelled)
                {
                    dueMessages.Add(immediateEntry.Message);
                    _messageIndex.TryRemove(immediateEntry.Message.MessageId, out _);
                }
            }
            
            // 2. 处理时间轮中到期的消息
            var expiredEntries = _timeWheel.AdvanceClock(currentTime);
            
            foreach (var entry in expiredEntries)
            {
                if (dueMessages.Count >= batchSize)
                {
                    // 超出批量大小，重新加入立即队列
                    _immediateQueue.Enqueue(entry);
                    continue;
                }
                
                if (!entry.IsCancelled)
                {
                    dueMessages.Add(entry.Message);
                    _messageIndex.TryRemove(entry.Message.MessageId, out _);
                }
            }
        }

        if (dueMessages.Count > 0)
        {
            _logger.LogDebug("Retrieved {Count} due messages", dueMessages.Count);
        }

        return Task.FromResult<IReadOnlyList<IMessage>>(dueMessages);
    }

    /// <inheritdoc />
    public Task<bool> RemoveAsync(string messageId, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return Task.FromResult(false);
        }

        bool removed;
        lock (_lock)
        {
            if (_messageIndex.TryRemove(messageId, out var entry))
            {
                entry.IsCancelled = true;
                _timeWheel.Cancel(messageId);
                removed = true;
                _logger.LogDebug("Cancelled delayed message {MessageId}", messageId);
            }
            else
            {
                removed = false;
            }
        }

        return Task.FromResult(removed);
    }

    /// <inheritdoc />
    public Task<long> GetPendingCountAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return Task.FromResult(0L);
        }

        return Task.FromResult(_timeWheel.GetPendingCount() + _immediateQueue.Count);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _timeWheel.Dispose();
        _messageIndex.Clear();
    }
}
