using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Storage;
using JZVerse.MicroHuaxia.MessageQueue.Storage.FileLog.Segments;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.MessageQueue.Storage.FileLog;

/// <summary>
/// FileLog 消息存储实现
/// </summary>
public sealed class FileLogMessageStore : IMessageStore, IDisposable
{
    private readonly ILogger<FileLogMessageStore> _logger;
    private readonly FileLogStoreOptions _options;

    // 分区管理: (topic, partition) -> LogPartition
    private readonly ConcurrentDictionary<(string Topic, int Partition), LogPartition> _partitions = new();

    // 消息ID索引: messageId -> (topic, partition, offset)
    private readonly ConcurrentDictionary<string, (string Topic, int Partition, long Offset)> _messageIndex = new();

    private readonly Timer _flushTimer;
    private bool _disposed;

    public FileLogMessageStore(ILogger<FileLogMessageStore> logger, FileLogStoreOptions options)
    {
        _logger = logger;
        _options = options;

        // 定时刷盘
        if (options.FlushPolicy == FlushPolicy.EverySecond)
        {
            _flushTimer = new Timer(FlushAll, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }
        else
        {
            _flushTimer = new Timer(_ => { }, null, Timeout.Infinite, Timeout.Infinite);
        }

        EnsureDataDirectoryExists();
    }

    /// <inheritdoc />
    public Task<long> AppendAsync(IMessage message, int partition = 0, CancellationToken cancellationToken = default)
    {
        var logPartition = GetOrCreatePartition(message.Topic, partition);
        var offset = logPartition.Append(message);

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
            var logPartition = GetOrCreatePartition(message.Topic, partition);
            var offset = logPartition.Append(message);

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

        var logPartition = GetPartition(location.Topic, location.Partition);
        if (logPartition == null)
        {
            return Task.FromResult<IMessage?>(null);
        }

        var message = logPartition.Read(location.Offset);
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
        var logPartition = GetPartition(topic, partition);
        if (logPartition == null)
        {
            return Task.FromResult<IReadOnlyList<IMessage>>([]);
        }

        var messages = logPartition.ReadRange(offset, count);
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
        var logPartition = GetPartition(topic, partition);
        if (logPartition == null)
        {
            return Task.FromResult<IReadOnlyList<IMessage>>([]);
        }

        var messages = logPartition.ReadByTimeRange(startTime, endTime);
        return Task.FromResult(messages);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string messageId, CancellationToken cancellationToken = default)
    {
        // FileLog 存储通常不支持删除单条消息
        // 而是通过保留策略清理过期数据
        return Task.FromResult(_messageIndex.TryRemove(messageId, out _));
    }

    /// <inheritdoc />
    public Task<long> GetLatestOffsetAsync(string topic, int partition, CancellationToken cancellationToken = default)
    {
        var logPartition = GetPartition(topic, partition);
        return Task.FromResult(logPartition?.LatestOffset ?? 0);
    }

    /// <inheritdoc />
    public Task<long> GetEarliestOffsetAsync(string topic, int partition, CancellationToken cancellationToken = default)
    {
        var logPartition = GetPartition(topic, partition);
        return Task.FromResult(logPartition?.EarliestOffset ?? 0);
    }

    private LogPartition GetOrCreatePartition(string topic, int partition)
    {
        return _partitions.GetOrAdd((topic, partition), key =>
        {
            var partitionPath = GetPartitionPath(key.Topic, key.Partition);
            return new LogPartition(partitionPath, _options, _logger);
        });
    }

    private LogPartition? GetPartition(string topic, int partition)
    {
        _partitions.TryGetValue((topic, partition), out var logPartition);
        return logPartition;
    }

    private string GetPartitionPath(string topic, int partition)
    {
        var safeTopic = topic.Replace("/", "_").Replace("\\", "_");
        return Path.Combine(_options.DataDirectory, $"{safeTopic}-{partition}");
    }

    private void EnsureDataDirectoryExists()
    {
        if (!Directory.Exists(_options.DataDirectory))
        {
            Directory.CreateDirectory(_options.DataDirectory);
        }
    }

    private void FlushAll(object? state)
    {
        foreach (var partition in _partitions.Values)
        {
            partition.Flush();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _flushTimer.Dispose();

        foreach (var partition in _partitions.Values)
        {
            partition.Dispose();
        }
    }
}

/// <summary>
/// 日志分区
/// </summary>
internal sealed class LogPartition : IDisposable
{
    private readonly string _basePath;
    private readonly FileLogStoreOptions _options;
    private readonly ILogger _logger;
    private readonly Lock _lock = new();

    private readonly List<LogSegment> _segments = new();
    private LogSegment? _activeSegment;

    public long LatestOffset => _activeSegment?.NextOffset ?? 0;
    public long EarliestOffset => _segments.Count > 0 ? _segments[0].BaseOffset : 0;

    public LogPartition(string basePath, FileLogStoreOptions options, ILogger logger)
    {
        _basePath = basePath;
        _options = options;
        _logger = logger;

        LoadSegments();
    }

    public long Append(IMessage message)
    {
        lock (_lock)
        {
            EnsureActiveSegment();

            var record = MessageRecord.FromMessage(message);
            var offset = _activeSegment!.Append(record);

            // 检查是否需要滚动到新段
            if (_activeSegment.IsFull)
            {
                RollSegment();
            }

            return offset;
        }
    }

    public IMessage? Read(long offset)
    {
        var segment = FindSegment(offset);
        if (segment == null)
        {
            return null;
        }

        var record = segment.Read(offset);
        return record?.ToMessage();
    }

    public IReadOnlyList<IMessage> ReadRange(long startOffset, int count)
    {
        var messages = new List<IMessage>(count);
        var currentOffset = startOffset;

        while (messages.Count < count)
        {
            var segment = FindSegment(currentOffset);
            if (segment == null)
            {
                break;
            }

            var remainingCount = count - messages.Count;
            var records = segment.ReadRange(currentOffset, remainingCount);

            foreach (var record in records)
            {
                messages.Add(record.ToMessage());
                currentOffset++;
            }

            // 如果这个段读完了，尝试下一个段
            if (records.Count < remainingCount && currentOffset < LatestOffset)
            {
                continue;
            }
            else
            {
                break;
            }
        }

        return messages;
    }

    public IReadOnlyList<IMessage> ReadByTimeRange(DateTimeOffset startTime, DateTimeOffset endTime)
    {
        var messages = new List<IMessage>();
        var startTimestamp = startTime.ToUnixTimeMilliseconds();
        var endTimestamp = endTime.ToUnixTimeMilliseconds();

        foreach (var segment in _segments)
        {
            var startOffset = segment.FindOffsetByTimestamp(startTimestamp);
            if (!startOffset.HasValue)
            {
                continue;
            }

            // 从找到的偏移量开始读取
            var records = segment.ReadRange(startOffset.Value, 1000); // 批量读取
            foreach (var record in records)
            {
                if (record.Timestamp >= startTimestamp && record.Timestamp <= endTimestamp)
                {
                    messages.Add(record.ToMessage());
                }
                else if (record.Timestamp > endTimestamp)
                {
                    break;
                }
            }
        }

        return messages;
    }

    public void Flush()
    {
        _activeSegment?.Flush();
    }

    public void Dispose()
    {
        foreach (var segment in _segments)
        {
            segment.Dispose();
        }
    }

    private void LoadSegments()
    {
        if (!Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
            return;
        }

        // 加载现有的日志段
        var logFiles = Directory.GetFiles(_basePath, "*.log")
            .OrderBy(f => f)
            .ToList();

        foreach (var logFile in logFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(logFile);
            if (long.TryParse(fileName, out var baseOffset))
            {
                var segmentPath = Path.Combine(_basePath, fileName);
                var segment = new LogSegment(segmentPath, baseOffset, _options, _logger);
                _segments.Add(segment);
            }
        }

        if (_segments.Count > 0)
        {
            _activeSegment = _segments[^1];
        }
    }

    private void EnsureActiveSegment()
    {
        if (_activeSegment == null || _activeSegment.IsFull)
        {
            RollSegment();
        }
    }

    private void RollSegment()
    {
        var baseOffset = _activeSegment?.NextOffset ?? 0;
        var segmentPath = Path.Combine(_basePath, baseOffset.ToString("D20"));

        _activeSegment = new LogSegment(segmentPath, baseOffset, _options, _logger);
        _segments.Add(_activeSegment);

        _logger.LogInformation("Rolled to new segment at offset {BaseOffset}", baseOffset);
    }

    private LogSegment? FindSegment(long offset)
    {
        // 二分查找包含该偏移量的段
        for (int i = _segments.Count - 1; i >= 0; i--)
        {
            if (_segments[i].BaseOffset <= offset)
            {
                return _segments[i];
            }
        }

        return null;
    }
}
