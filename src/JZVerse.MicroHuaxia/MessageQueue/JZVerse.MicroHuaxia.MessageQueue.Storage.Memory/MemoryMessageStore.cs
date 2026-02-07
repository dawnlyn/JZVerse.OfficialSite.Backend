using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Storage;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.MessageQueue.Storage.Memory;

/// <summary>
/// 内存消息存储实现
/// </summary>
public sealed class MemoryMessageStore : IMessageStore
{
    private readonly ILogger<MemoryMessageStore> _logger;
    private readonly MemoryStoreOptions _options;

    // 按主题和分区存储消息: topic -> partition -> (offset, message)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, MessagePartition>> _partitions = new();

    // 消息ID索引: messageId -> (topic, partition, offset)
    private readonly ConcurrentDictionary<string, (string Topic, int Partition, long Offset)> _messageIndex = new();

    public MemoryMessageStore(ILogger<MemoryMessageStore> logger, MemoryStoreOptions? options = null)
    {
        _logger = logger;
        _options = options ?? new MemoryStoreOptions();
    }

    /// <inheritdoc />
    public Task<long> AppendAsync(IMessage message, int partition = 0, CancellationToken cancellationToken = default)
    {
        var topicPartitions = _partitions.GetOrAdd(message.Topic, _ => new ConcurrentDictionary<int, MessagePartition>());
        var messagePartition = topicPartitions.GetOrAdd(partition, _ => new MessagePartition(_options.MaxMessagesPerPartition));

        var offset = messagePartition.Append(message);
        _messageIndex[message.MessageId] = (message.Topic, partition, offset);

        _logger.LogDebug("Message {MessageId} appended to {Topic}:{Partition} at offset {Offset}",
            message.MessageId, message.Topic, partition, offset);

        return Task.FromResult(offset);
    }

    /// <inheritdoc />
    public Task<long> AppendBatchAsync(IEnumerable<IMessage> messages, int partition = 0, CancellationToken cancellationToken = default)
    {
        long firstOffset = -1;
        foreach (var message in messages)
        {
            var topicPartitions = _partitions.GetOrAdd(message.Topic, _ => new ConcurrentDictionary<int, MessagePartition>());
            var messagePartition = topicPartitions.GetOrAdd(partition, _ => new MessagePartition(_options.MaxMessagesPerPartition));

            var offset = messagePartition.Append(message);
            _messageIndex[message.MessageId] = (message.Topic, partition, offset);

            if (firstOffset == -1)
            {
                firstOffset = offset;
            }
        }

        return Task.FromResult(firstOffset);
    }

    /// <inheritdoc />
    public Task<IMessage?> GetByIdAsync(string messageId, CancellationToken cancellationToken = default)
    {
        if (!_messageIndex.TryGetValue(messageId, out var location))
        {
            return Task.FromResult<IMessage?>(null);
        }

        if (!_partitions.TryGetValue(location.Topic, out var topicPartitions))
        {
            return Task.FromResult<IMessage?>(null);
        }

        if (!topicPartitions.TryGetValue(location.Partition, out var partition))
        {
            return Task.FromResult<IMessage?>(null);
        }

        var message = partition.GetByOffset(location.Offset);
        return Task.FromResult(message);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IMessage>> GetByOffsetAsync(
        string topic,
        int partition,
        long offset,
        int count,
        CancellationToken cancellationToken = default)
    {
        if (!_partitions.TryGetValue(topic, out var topicPartitions))
        {
            return Task.FromResult<IReadOnlyList<IMessage>>([]);
        }

        if (!topicPartitions.TryGetValue(partition, out var messagePartition))
        {
            return Task.FromResult<IReadOnlyList<IMessage>>([]);
        }

        var messages = messagePartition.GetByOffsetRange(offset, count);
        return Task.FromResult(messages);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IMessage>> GetByTimeRangeAsync(
        string topic,
        int partition,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default)
    {
        if (!_partitions.TryGetValue(topic, out var topicPartitions))
        {
            return Task.FromResult<IReadOnlyList<IMessage>>([]);
        }

        if (!topicPartitions.TryGetValue(partition, out var messagePartition))
        {
            return Task.FromResult<IReadOnlyList<IMessage>>([]);
        }

        var messages = messagePartition.GetByTimeRange(startTime, endTime);
        return Task.FromResult(messages);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string messageId, CancellationToken cancellationToken = default)
    {
        if (!_messageIndex.TryRemove(messageId, out var location))
        {
            return Task.FromResult(false);
        }

        if (!_partitions.TryGetValue(location.Topic, out var topicPartitions))
        {
            return Task.FromResult(false);
        }

        if (!topicPartitions.TryGetValue(location.Partition, out var partition))
        {
            return Task.FromResult(false);
        }

        var deleted = partition.Delete(location.Offset);
        _logger.LogDebug("Message {MessageId} deleted: {Deleted}", messageId, deleted);
        return Task.FromResult(deleted);
    }

    /// <inheritdoc />
    public Task<long> GetLatestOffsetAsync(string topic, int partition, CancellationToken cancellationToken = default)
    {
        if (!_partitions.TryGetValue(topic, out var topicPartitions))
        {
            return Task.FromResult(0L);
        }

        if (!topicPartitions.TryGetValue(partition, out var messagePartition))
        {
            return Task.FromResult(0L);
        }

        return Task.FromResult(messagePartition.LatestOffset);
    }

    /// <inheritdoc />
    public Task<long> GetEarliestOffsetAsync(string topic, int partition, CancellationToken cancellationToken = default)
    {
        if (!_partitions.TryGetValue(topic, out var topicPartitions))
        {
            return Task.FromResult(0L);
        }

        if (!topicPartitions.TryGetValue(partition, out var messagePartition))
        {
            return Task.FromResult(0L);
        }

        return Task.FromResult(messagePartition.EarliestOffset);
    }

    /// <summary>
    /// 内部分区存储类
    /// </summary>
    private sealed class MessagePartition
    {
        private readonly int _maxMessages;
        private readonly Lock _lock = new();
        private readonly SortedDictionary<long, IMessage> _messages = new();
        private readonly HashSet<long> _deletedOffsets = new();
        private long _nextOffset = 0;

        public long LatestOffset => _nextOffset;
        public long EarliestOffset
        {
            get
            {
                lock (_lock)
                {
                    return _messages.Count > 0 ? _messages.Keys.First() : 0;
                }
            }
        }

        public MessagePartition(int maxMessages)
        {
            _maxMessages = maxMessages;
        }

        public long Append(IMessage message)
        {
            lock (_lock)
            {
                var offset = _nextOffset++;
                _messages[offset] = message;

                // 容量管理：超过限制时删除最早的消息
                while (_messages.Count > _maxMessages)
                {
                    var firstKey = _messages.Keys.First();
                    _messages.Remove(firstKey);
                }

                return offset;
            }
        }

        public IMessage? GetByOffset(long offset)
        {
            lock (_lock)
            {
                if (_deletedOffsets.Contains(offset))
                {
                    return null;
                }

                return _messages.TryGetValue(offset, out var message) ? message : null;
            }
        }

        public IReadOnlyList<IMessage> GetByOffsetRange(long startOffset, int count)
        {
            lock (_lock)
            {
                var result = new List<IMessage>(count);
                foreach (var kvp in _messages.Where(m => m.Key >= startOffset && !_deletedOffsets.Contains(m.Key)).Take(count))
                {
                    result.Add(kvp.Value);
                }
                return result;
            }
        }

        public IReadOnlyList<IMessage> GetByTimeRange(DateTimeOffset startTime, DateTimeOffset endTime)
        {
            lock (_lock)
            {
                return _messages.Values
                    .Where(m => m.Timestamp >= startTime && m.Timestamp <= endTime)
                    .ToList();
            }
        }

        public bool Delete(long offset)
        {
            lock (_lock)
            {
                if (!_messages.ContainsKey(offset))
                {
                    return false;
                }

                _deletedOffsets.Add(offset);
                return true;
            }
        }
    }
}
