using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.Security.Audit;

namespace JZVerse.MicroHuaxia.Security.Storage;

/// <summary>
/// 内存审计存储（用于开发和测试）
/// </summary>
public sealed class InMemoryAuditStore : IAuditStore
{
    private readonly ConcurrentBag<AuditEvent> _events = new();
    private readonly ConcurrentDictionary<string, AuditEvent> _eventsById = new();

    /// <inheritdoc />
    public Task StoreBatchAsync(IReadOnlyList<AuditEvent> events)
    {
        foreach (var auditEvent in events)
        {
            _events.Add(auditEvent);
            _eventsById[auditEvent.EventId] = auditEvent;
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditEvent>> QueryAsync(
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        string? eventType = null,
        string? subjectId = null,
        int limit = 100)
    {
        var query = _events.AsEnumerable();

        query = query.Where(e => e.Timestamp >= startTime && e.Timestamp <= endTime);

        if (!string.IsNullOrEmpty(eventType))
        {
            query = query.Where(e => e.EventType == eventType);
        }

        if (!string.IsNullOrEmpty(subjectId))
        {
            query = query.Where(e => e.Subject?.IdentityId == subjectId);
        }

        var result = query
            .OrderByDescending(e => e.Timestamp)
            .Take(limit)
            .ToList();

        return Task.FromResult<IReadOnlyList<AuditEvent>>(result);
    }

    /// <summary>
    /// 获取所有事件（仅用于调试）
    /// </summary>
    public IReadOnlyList<AuditEvent> GetAllEvents()
    {
        return _events.OrderByDescending(e => e.Timestamp).ToList();
    }

    /// <summary>
    /// 清空所有事件（仅用于测试）
    /// </summary>
    public void Clear()
    {
        _events.Clear();
        _eventsById.Clear();
    }
}
