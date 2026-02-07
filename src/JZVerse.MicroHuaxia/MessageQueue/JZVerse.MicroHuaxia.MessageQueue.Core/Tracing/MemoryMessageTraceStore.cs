using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Tracing;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.MessageQueue.Core.Tracing;

/// <summary>
/// 追踪存储配置
/// </summary>
public sealed class MessageTraceStoreOptions
{
    /// <summary>
    /// 最大保留条目数
    /// </summary>
    public int MaxEntries { get; set; } = 100000;
    
    /// <summary>
    /// 数据保留时间
    /// </summary>
    public TimeSpan Retention { get; set; } = TimeSpan.FromHours(24);
    
    /// <summary>
    /// 清理间隔
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(30);
}

/// <summary>
/// 内存消息追踪存储
/// </summary>
public sealed class MemoryMessageTraceStore : IMessageTraceStore, IDisposable
{
    private readonly ILogger<MemoryMessageTraceStore> _logger;
    private readonly MessageTraceStoreOptions _options;
    
    // 按消息ID索引
    private readonly ConcurrentDictionary<string, ConcurrentBag<MessageTracePoint>> _byMessageId = new();
    
    // 按主题索引
    private readonly ConcurrentDictionary<string, ConcurrentBag<MessageTracePoint>> _byTopic = new();
    
    // 按消费者组索引
    private readonly ConcurrentDictionary<string, ConcurrentBag<MessageTracePoint>> _byConsumerGroup = new();
    
    // 所有追踪点（用于清理）
    private readonly ConcurrentQueue<MessageTracePoint> _allTracePoints = new();
    
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    public MemoryMessageTraceStore(
        ILogger<MemoryMessageTraceStore> logger,
        MessageTraceStoreOptions? options = null)
    {
        _logger = logger;
        _options = options ?? new MessageTraceStoreOptions();
        
        // 启动清理定时器
        _cleanupTimer = new Timer(
            CleanupCallback,
            null,
            _options.CleanupInterval,
            _options.CleanupInterval);
    }

    /// <inheritdoc />
    public Task StoreAsync(MessageTracePoint tracePoint, CancellationToken cancellationToken = default)
    {
        if (_disposed) return Task.CompletedTask;
        
        // 添加到消息ID索引
        var messagePoints = _byMessageId.GetOrAdd(tracePoint.MessageId, _ => new ConcurrentBag<MessageTracePoint>());
        messagePoints.Add(tracePoint);
        
        // 添加到主题索引
        if (!string.IsNullOrEmpty(tracePoint.Topic))
        {
            var topicPoints = _byTopic.GetOrAdd(tracePoint.Topic, _ => new ConcurrentBag<MessageTracePoint>());
            topicPoints.Add(tracePoint);
        }
        
        // 添加到消费者组索引
        if (!string.IsNullOrEmpty(tracePoint.ConsumerGroup))
        {
            var groupPoints = _byConsumerGroup.GetOrAdd(tracePoint.ConsumerGroup, _ => new ConcurrentBag<MessageTracePoint>());
            groupPoints.Add(tracePoint);
        }
        
        // 添加到全局队列
        _allTracePoints.Enqueue(tracePoint);
        
        _logger.LogDebug("Stored trace point: {Type} for message {MessageId} at {NodeId}",
            tracePoint.Type, tracePoint.MessageId, tracePoint.NodeId);
        
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<MessageTracePoint>> GetByMessageIdAsync(
        string messageId,
        CancellationToken cancellationToken = default)
    {
        if (_byMessageId.TryGetValue(messageId, out var points))
        {
            var sortedPoints = points
                .Where(p => !IsExpired(p))
                .OrderBy(p => p.Timestamp)
                .ToList();
            return Task.FromResult<IReadOnlyList<MessageTracePoint>>(sortedPoints);
        }
        
        return Task.FromResult<IReadOnlyList<MessageTracePoint>>(Array.Empty<MessageTracePoint>());
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<MessageTracePoint>> QueryAsync(
        string? topic,
        string? consumerGroup,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        int limit = 1000,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<MessageTracePoint> query;
        
        if (!string.IsNullOrEmpty(topic) && _byTopic.TryGetValue(topic, out var topicPoints))
        {
            query = topicPoints;
        }
        else if (!string.IsNullOrEmpty(consumerGroup) && _byConsumerGroup.TryGetValue(consumerGroup, out var groupPoints))
        {
            query = groupPoints;
        }
        else
        {
            // 全量查询
            query = _allTracePoints;
        }
        
        var result = query
            .Where(p => p.Timestamp >= startTime && p.Timestamp <= endTime)
            .Where(p => string.IsNullOrEmpty(topic) || p.Topic == topic)
            .Where(p => string.IsNullOrEmpty(consumerGroup) || p.ConsumerGroup == consumerGroup)
            .OrderByDescending(p => p.Timestamp)
            .Take(limit)
            .ToList();
        
        return Task.FromResult<IReadOnlyList<MessageTracePoint>>(result);
    }

    /// <inheritdoc />
    public Task CleanupAsync(TimeSpan retention, CancellationToken cancellationToken = default)
    {
        var cutoffTime = DateTimeOffset.UtcNow - retention;
        var removedCount = 0;
        
        // 清理过期条目
        foreach (var kvp in _byMessageId)
        {
            var validPoints = kvp.Value.Where(p => p.Timestamp >= cutoffTime).ToList();
            if (validPoints.Count < kvp.Value.Count)
            {
                _byMessageId[kvp.Key] = new ConcurrentBag<MessageTracePoint>(validPoints);
                removedCount += kvp.Value.Count - validPoints.Count;
            }
            
            // 如果消息ID下没有追踪点了，移除整个条目
            if (validPoints.Count == 0)
            {
                _byMessageId.TryRemove(kvp.Key, out _);
            }
        }
        
        // 清理主题索引
        foreach (var kvp in _byTopic)
        {
            var validPoints = kvp.Value.Where(p => p.Timestamp >= cutoffTime).ToList();
            if (validPoints.Count == 0)
            {
                _byTopic.TryRemove(kvp.Key, out _);
            }
            else if (validPoints.Count < kvp.Value.Count)
            {
                _byTopic[kvp.Key] = new ConcurrentBag<MessageTracePoint>(validPoints);
            }
        }
        
        // 清理消费者组索引
        foreach (var kvp in _byConsumerGroup)
        {
            var validPoints = kvp.Value.Where(p => p.Timestamp >= cutoffTime).ToList();
            if (validPoints.Count == 0)
            {
                _byConsumerGroup.TryRemove(kvp.Key, out _);
            }
            else if (validPoints.Count < kvp.Value.Count)
            {
                _byConsumerGroup[kvp.Key] = new ConcurrentBag<MessageTracePoint>(validPoints);
            }
        }
        
        // 清理全局队列（尽力而为）
        while (_allTracePoints.TryPeek(out var point) && point.Timestamp < cutoffTime)
        {
            _allTracePoints.TryDequeue(out _);
        }
        
        if (removedCount > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired trace points", removedCount);
        }
        
        return Task.CompletedTask;
    }

    private bool IsExpired(MessageTracePoint point)
    {
        return point.Timestamp < DateTimeOffset.UtcNow - _options.Retention;
    }

    private async void CleanupCallback(object? state)
    {
        if (_disposed) return;
        
        try
        {
            await CleanupAsync(_options.Retention);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during trace cleanup");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _cleanupTimer.Dispose();
        _byMessageId.Clear();
        _byTopic.Clear();
        _byConsumerGroup.Clear();
    }
}
