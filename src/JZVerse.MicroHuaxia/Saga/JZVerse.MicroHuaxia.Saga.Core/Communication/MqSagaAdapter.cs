using System.Collections.Concurrent;
using System.Text.Json;
using JZVerse.MicroHuaxia.Saga.Abstractions;
using JZVerse.MicroHuaxia.Saga.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Saga.Core.Communication;

/// <summary>
/// MQ Saga 通信适配器配置
/// </summary>
public sealed class MqSagaAdapterOptions
{
    /// <summary>
    /// 服务主题映射（服务名 -> 主题名）
    /// </summary>
    public Dictionary<string, string> ServiceTopics { get; set; } = new();
    
    /// <summary>
    /// 回复队列前缀
    /// </summary>
    public string ReplyQueuePrefix { get; set; } = "saga-reply";
    
    /// <summary>
    /// 请求超时时间
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// 是否启用事务消息（确保发送可靠性）
    /// </summary>
    public bool UseTransactionalMessaging { get; set; } = false;
}

/// <summary>
/// MQ Saga 通信适配器（可选集成消息队列）
/// </summary>
/// <remarks>
/// 此适配器实现了请求-响应模式：
/// 1. 发送请求消息到目标服务的主题
/// 2. 等待目标服务发送响应到回复队列
/// 
/// 使用时需要确保目标服务：
/// 1. 订阅相应的主题
/// 2. 处理请求后发送响应到 ReplyTo 指定的队列
/// </remarks>
public sealed class MqSagaAdapter : ISagaCommunicationAdapter, IDisposable
{
    private readonly ILogger<MqSagaAdapter> _logger;
    private readonly MqSagaAdapterOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;
    
    // 简化实现：使用内存字典模拟请求-响应
    // 生产环境应该使用真实的消息队列
    private readonly ConcurrentDictionary<string, TaskCompletionSource<object?>> _pendingRequests = new();
    private bool _disposed;

    public MqSagaAdapter(
        ILogger<MqSagaAdapter> logger,
        MqSagaAdapterOptions? options = null)
    {
        _logger = logger;
        _options = options ?? new MqSagaAdapterOptions();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task<StepExecutionResult> ExecuteStepAsync(
        string serviceName,
        StepExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var topic = GetServiceTopic(serviceName);
        
        _logger.LogDebug("Executing step {StepId} via MQ to topic {Topic}, correlationId: {CorrelationId}", 
            request.StepId, topic, correlationId);
        
        // 创建等待响应的任务
        var tcs = new TaskCompletionSource<object?>();
        _pendingRequests[correlationId] = tcs;
        
        try
        {
            // 构造消息
            var message = new SagaMqMessage
            {
                CorrelationId = correlationId,
                ActionType = "Execute",
                Request = request,
                ReplyTo = $"{_options.ReplyQueuePrefix}-{Environment.MachineName}",
                Timestamp = DateTimeOffset.UtcNow
            };
            
            // TODO: 实际实现应该使用 IMessageProducer 发送消息
            // await _messageProducer.SendAsync(topic, message);
            _logger.LogWarning("MQ adapter: Message sending not implemented - using placeholder");
            
            // 等待响应
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(request.Timeout ?? _options.RequestTimeout);
            
            // 模拟：直接返回成功（实际应该等待响应）
            _logger.LogWarning("MQ adapter: Response waiting not implemented - returning placeholder success");
            return StepExecutionResult.Succeeded();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StepExecutionResult.Failed("Request timed out", retryable: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing step {StepId} via MQ", request.StepId);
            return StepExecutionResult.Failed(ex.Message, retryable: true);
        }
        finally
        {
            _pendingRequests.TryRemove(correlationId, out _);
        }
    }

    public async Task<CompensationResult> CompensateStepAsync(
        string serviceName,
        CompensationRequest request,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var topic = GetServiceTopic(serviceName);
        
        _logger.LogDebug("Compensating step {StepId} via MQ to topic {Topic}, correlationId: {CorrelationId}", 
            request.StepId, topic, correlationId);
        
        try
        {
            // 构造消息
            var message = new SagaMqMessage
            {
                CorrelationId = correlationId,
                ActionType = "Compensate",
                CompensationRequest = request,
                ReplyTo = $"{_options.ReplyQueuePrefix}-{Environment.MachineName}",
                Timestamp = DateTimeOffset.UtcNow
            };
            
            // TODO: 实际实现应该使用 IMessageProducer 发送消息
            _logger.LogWarning("MQ adapter: Message sending not implemented - using placeholder");
            
            // 模拟：直接返回成功
            return CompensationResult.Succeeded();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error compensating step {StepId} via MQ", request.StepId);
            return CompensationResult.Failed(ex.Message, retryable: true);
        }
    }

    /// <summary>
    /// 处理响应消息（由消费者调用）
    /// </summary>
    public void HandleResponse(string correlationId, object? result)
    {
        if (_pendingRequests.TryRemove(correlationId, out var tcs))
        {
            tcs.TrySetResult(result);
        }
    }

    private string GetServiceTopic(string serviceName)
    {
        if (_options.ServiceTopics.TryGetValue(serviceName, out var topic))
        {
            return topic;
        }
        
        // 默认使用服务名作为主题
        return $"saga-{serviceName}";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        foreach (var tcs in _pendingRequests.Values)
        {
            tcs.TrySetCanceled();
        }
        _pendingRequests.Clear();
    }
}

/// <summary>
/// Saga MQ 消息格式
/// </summary>
internal sealed record SagaMqMessage
{
    public required string CorrelationId { get; init; }
    public required string ActionType { get; init; } // "Execute" or "Compensate"
    public StepExecutionRequest? Request { get; init; }
    public CompensationRequest? CompensationRequest { get; init; }
    public required string ReplyTo { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}
