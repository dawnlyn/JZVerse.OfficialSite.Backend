using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Security.Audit;

/// <summary>
/// 审计日志记录器实现
/// </summary>
public sealed class AuditLogger : IAuditLogger
{
    private readonly IAuditStore _auditStore;
    private readonly ILogger<AuditLogger> _logger;

    // 异步队列
    private readonly BlockingCollection<AuditEvent> _eventQueue;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Task _processingTask;

    public AuditLogger(
        IAuditStore auditStore,
        ILogger<AuditLogger> logger)
    {
        _auditStore = auditStore;
        _logger = logger;
        _eventQueue = new BlockingCollection<AuditEvent>(new ConcurrentQueue<AuditEvent>(), 10000);
        _cancellationTokenSource = new CancellationTokenSource();
        _processingTask = Task.Run(ProcessEventsAsync);
    }

    /// <inheritdoc />
    public Task LogAccessAsync(
        AccessRequest request,
        AccessDecision decision,
        IReadOnlyDictionary<string, object>? properties = null)
    {
        var auditEvent = new AuditEvent
        {
            EventType = "access_request",
            Level = decision.Allowed ? AuditLevel.Info : AuditLevel.Security,
            Subject = request.Subject,
            Resource = request.Resource,
            Action = request.Action,
            Success = decision.Allowed,
            ResultDetail = decision.Reason,
            RequestId = request.RequestId,
            TraceId = request.TraceId,
            Properties = properties ?? new Dictionary<string, object>()
        };

        EnqueueEvent(auditEvent);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task LogAuthenticationAsync(
        SecurityIdentity? identity,
        AuthenticationMethod method,
        bool success,
        string? reason = null,
        IReadOnlyDictionary<string, object>? properties = null)
    {
        var auditEvent = new AuditEvent
        {
            EventType = "authentication",
            Level = success ? AuditLevel.Info : AuditLevel.Security,
            Subject = identity,
            Action = method.ToString(),
            Success = success,
            ResultDetail = reason,
            Properties = properties ?? new Dictionary<string, object>()
        };

        EnqueueEvent(auditEvent);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task LogOperationAsync(
        string eventType,
        AuditLevel level,
        SecurityIdentity? identity,
        string? resource = null,
        string? action = null,
        bool success = true,
        IReadOnlyDictionary<string, object>? properties = null)
    {
        var auditEvent = new AuditEvent
        {
            EventType = eventType,
            Level = level,
            Subject = identity,
            Resource = resource,
            Action = action,
            Success = success,
            Properties = properties ?? new Dictionary<string, object>()
        };

        EnqueueEvent(auditEvent);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task LogSecurityEventAsync(
        string eventType,
        SecurityEventSeverity severity,
        string message,
        SecurityIdentity? identity = null,
        IReadOnlyDictionary<string, object>? properties = null)
    {
        var level = severity switch
        {
            SecurityEventSeverity.Info => AuditLevel.Info,
            SecurityEventSeverity.Low => AuditLevel.Info,
            SecurityEventSeverity.Medium => AuditLevel.Important,
            SecurityEventSeverity.High => AuditLevel.Sensitive,
            SecurityEventSeverity.Critical => AuditLevel.Security,
            _ => AuditLevel.Info
        };

        var auditEvent = new AuditEvent
        {
            EventType = eventType,
            Level = level,
            Subject = identity,
            Success = false,
            ResultDetail = message,
            Properties = properties ?? new Dictionary<string, object>()
        };

        EnqueueEvent(auditEvent);

        // 安全事件立即记录到日志
        _logger.LogWarning(
            "安全事件: {EventType}, 级别: {Severity}, 消息: {Message}",
            eventType, severity, message);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task FlushAsync()
    {
        // 等待队列处理完成
        while (_eventQueue.Count > 0)
        {
            await Task.Delay(100);
        }
    }

    /// <summary>
    /// 将事件加入队列
    /// </summary>
    private void EnqueueEvent(AuditEvent auditEvent)
    {
        try
        {
            if (!_eventQueue.TryAdd(auditEvent, TimeSpan.FromSeconds(1)))
            {
                _logger.LogWarning("审计事件队列已满，事件被丢弃: {EventType}", auditEvent.EventType);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加审计事件到队列失败");
        }
    }

    /// <summary>
    /// 异步处理事件队列
    /// </summary>
    private async Task ProcessEventsAsync()
    {
        var batch = new List<AuditEvent>(100);

        try
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                // 等待事件或超时
                if (_eventQueue.TryTake(out var auditEvent, TimeSpan.FromSeconds(1)))
                {
                    batch.Add(auditEvent);

                    // 批量处理
                    while (batch.Count < 100 && _eventQueue.TryTake(out var nextEvent, TimeSpan.Zero))
                    {
                        batch.Add(nextEvent);
                    }

                    // 存储审计事件
                    await StoreBatchAsync(batch);
                    batch.Clear();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "审计事件处理循环异常");
        }
    }

    /// <summary>
    /// 批量存储审计事件
    /// </summary>
    private async Task StoreBatchAsync(List<AuditEvent> batch)
    {
        try
        {
            // 计算完整性哈希
            var eventsWithIntegrity = batch.Select(CalculateIntegrity).ToList();

            // 存储到审计存储
            await _auditStore.StoreBatchAsync(eventsWithIntegrity);

            _logger.LogDebug("已存储 {Count} 条审计事件", batch.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "存储审计事件失败");
        }
    }

    /// <summary>
    /// 计算审计事件的完整性哈希
    /// </summary>
    private AuditEvent CalculateIntegrity(AuditEvent auditEvent)
    {
        // 序列化事件（不包含完整性字段）
        var eventData = auditEvent with { IntegrityHash = null, Signature = null };
        string json = JsonSerializer.Serialize(eventData);
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);

        // 计算哈希
        var sm3 = new Sm3Provider();
        byte[] hash = sm3.Hash(bytes);
        string hashString = Convert.ToHexString(hash).ToLowerInvariant();

        return auditEvent with { IntegrityHash = hashString };
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _eventQueue.CompleteAdding();
        _processingTask.Wait(TimeSpan.FromSeconds(5));
        _eventQueue.Dispose();
        _cancellationTokenSource.Dispose();
    }
}

/// <summary>
/// 审计存储接口
/// </summary>
public interface IAuditStore
{
    /// <summary>
    /// 批量存储审计事件
    /// </summary>
    Task StoreBatchAsync(IReadOnlyList<AuditEvent> events);

    /// <summary>
    /// 查询审计事件
    /// </summary>
    Task<IReadOnlyList<AuditEvent>> QueryAsync(
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        string? eventType = null,
        string? subjectId = null,
        int limit = 100);
}
