using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Storage;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.MessageQueue.Storage.Memory;

/// <summary>
/// 内存偏移量管理器实现
/// </summary>
public sealed class MemoryOffsetManager : IOffsetManager
{
    private readonly ILogger<MemoryOffsetManager> _logger;
    private readonly IMessageStore _messageStore;

    // 消费位点存储: (consumerGroup, topic, partition) -> offset
    private readonly ConcurrentDictionary<(string ConsumerGroup, string Topic, int Partition), long> _offsets = new();

    public MemoryOffsetManager(ILogger<MemoryOffsetManager> logger, IMessageStore messageStore)
    {
        _logger = logger;
        _messageStore = messageStore;
    }

    /// <inheritdoc />
    public Task<long> GetOffsetAsync(
        string consumerGroup,
        string topic,
        int partition,
        CancellationToken cancellationToken = default)
    {
        var key = (consumerGroup, topic, partition);
        var offset = _offsets.GetValueOrDefault(key, -1);
        return Task.FromResult(offset);
    }

    /// <inheritdoc />
    public Task CommitOffsetAsync(
        string consumerGroup,
        string topic,
        int partition,
        long offset,
        CancellationToken cancellationToken = default)
    {
        var key = (consumerGroup, topic, partition);
        _offsets[key] = offset;

        _logger.LogDebug("Committed offset {Offset} for {ConsumerGroup}/{Topic}:{Partition}",
            offset, consumerGroup, topic, partition);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ResetOffsetAsync(
        string consumerGroup,
        string topic,
        int partition,
        long offset,
        CancellationToken cancellationToken = default)
    {
        var key = (consumerGroup, topic, partition);
        _offsets[key] = offset;

        _logger.LogInformation("Reset offset to {Offset} for {ConsumerGroup}/{Topic}:{Partition}",
            offset, consumerGroup, topic, partition);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<long> GetLagAsync(
        string consumerGroup,
        string topic,
        int partition,
        CancellationToken cancellationToken = default)
    {
        var committedOffset = await GetOffsetAsync(consumerGroup, topic, partition, cancellationToken);
        var latestOffset = await _messageStore.GetLatestOffsetAsync(topic, partition, cancellationToken);

        // 如果还没有提交过偏移量，lag 等于最新偏移量
        if (committedOffset < 0)
        {
            return latestOffset;
        }

        return Math.Max(0, latestOffset - committedOffset - 1);
    }
}
