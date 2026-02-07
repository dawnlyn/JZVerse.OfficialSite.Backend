using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Delay;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.MessageQueue.Core.Delay;

/// <summary>
/// 延迟消息调度器配置
/// </summary>
public sealed class DelaySchedulerOptions
{
    /// <summary>
    /// 时间轮配置
    /// </summary>
    public TimeWheelOptions TimeWheel { get; set; } = new();
    
    /// <summary>
    /// Tick 间隔（毫秒）
    /// </summary>
    public int TickIntervalMs { get; set; } = 100;
    
    /// <summary>
    /// 每次处理的最大消息数
    /// </summary>
    public int BatchSize { get; set; } = 100;
    
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// 基于时间轮的延迟消息调度器
/// </summary>
public sealed class TimeWheelDelayScheduler : IDelayMessageScheduler, IAsyncDisposable
{
    private readonly ILogger<TimeWheelDelayScheduler> _logger;
    private readonly IDelayMessageStore _delayStore;
    private readonly IMessageStore _messageStore;
    private readonly DelaySchedulerOptions _options;
    
    private Timer? _tickTimer;
    private bool _running;
    private readonly object _lock = new();
    private bool _disposed;
    
    /// <summary>
    /// 消息到期回调
    /// </summary>
    public Func<IMessage, CancellationToken, Task>? OnMessageDue { get; set; }

    public TimeWheelDelayScheduler(
        ILogger<TimeWheelDelayScheduler> logger,
        IDelayMessageStore delayStore,
        IMessageStore messageStore,
        DelaySchedulerOptions options)
    {
        _logger = logger;
        _delayStore = delayStore;
        _messageStore = messageStore;
        _options = options;
    }

    /// <inheritdoc />
    public Task ScheduleAsync(IMessage message, TimeSpan delay, CancellationToken cancellationToken = default)
    {
        var deliveryTime = DateTimeOffset.UtcNow.Add(delay);
        return ScheduleAtAsync(message, deliveryTime, cancellationToken);
    }

    /// <inheritdoc />
    public Task ScheduleAsync(IMessage message, DelayLevel level, CancellationToken cancellationToken = default)
    {
        return ScheduleAsync(message, level.ToTimeSpan(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task ScheduleAtAsync(IMessage message, DateTimeOffset deliveryTime, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TimeWheelDelayScheduler));
        }

        if (deliveryTime <= DateTimeOffset.UtcNow)
        {
            // 已到期，直接投递
            _logger.LogDebug("Message {MessageId} delivery time already passed, delivering immediately",
                message.MessageId);
            await DeliverMessageAsync(message, cancellationToken);
            return;
        }

        await _delayStore.AddAsync(message, deliveryTime, cancellationToken);
        
        _logger.LogInformation("Scheduled delayed message {MessageId} for {DeliveryTime}, delay: {Delay}",
            message.MessageId, deliveryTime, deliveryTime - DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public Task<bool> CancelAsync(string messageId, CancellationToken cancellationToken = default)
    {
        return _delayStore.RemoveAsync(messageId, cancellationToken);
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Delay scheduler is disabled");
            return Task.CompletedTask;
        }

        lock (_lock)
        {
            if (_running)
            {
                return Task.CompletedTask;
            }

            _running = true;
            
            // 启动 tick 定时器
            _tickTimer = new Timer(
                TickCallback,
                null,
                _options.TickIntervalMs,
                _options.TickIntervalMs);
        }

        _logger.LogInformation("Delay scheduler started with tick interval {TickInterval}ms",
            _options.TickIntervalMs);
        
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_running)
            {
                return Task.CompletedTask;
            }

            _running = false;
            _tickTimer?.Dispose();
            _tickTimer = null;
        }

        _logger.LogInformation("Delay scheduler stopped");
        return Task.CompletedTask;
    }

    private async void TickCallback(object? state)
    {
        if (!_running || _disposed)
        {
            return;
        }

        try
        {
            await ProcessDueMessagesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing due messages");
        }
    }

    private async Task ProcessDueMessagesAsync(CancellationToken cancellationToken)
    {
        var dueMessages = await _delayStore.GetDueMessagesAsync(_options.BatchSize, cancellationToken);
        
        if (dueMessages.Count == 0)
        {
            return;
        }

        _logger.LogDebug("Processing {Count} due delayed messages", dueMessages.Count);

        foreach (var message in dueMessages)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await DeliverMessageAsync(message, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deliver delayed message {MessageId}", message.MessageId);
            }
        }
    }

    private async Task DeliverMessageAsync(IMessage message, CancellationToken cancellationToken)
    {
        // 如果有自定义回调，使用回调
        if (OnMessageDue != null)
        {
            await OnMessageDue(message, cancellationToken);
            _logger.LogDebug("Delivered delayed message {MessageId} via callback", message.MessageId);
            return;
        }

        // 默认行为：写入正式存储
        var partition = CalculatePartition(message);
        await _messageStore.AppendAsync(message, partition, cancellationToken);
        
        _logger.LogDebug("Delivered delayed message {MessageId} to topic {Topic}:{Partition}",
            message.MessageId, message.Topic, partition);
    }

    private static int CalculatePartition(IMessage message, int partitionCount = 4)
    {
        if (!string.IsNullOrEmpty(message.PartitionKey))
        {
            return Math.Abs(message.PartitionKey.GetHashCode()) % partitionCount;
        }
        return Random.Shared.Next(partitionCount);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await StopAsync(CancellationToken.None);
        
        if (_delayStore is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

/// <summary>
/// 延迟消息处理后台服务
/// </summary>
public sealed class DelayMessageService : BackgroundService
{
    private readonly ILogger<DelayMessageService> _logger;
    private readonly IDelayMessageScheduler _scheduler;
    private readonly DelaySchedulerOptions _options;

    public DelayMessageService(
        ILogger<DelayMessageService> logger,
        IDelayMessageScheduler scheduler,
        DelaySchedulerOptions options)
    {
        _logger = logger;
        _scheduler = scheduler;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Delay message service is disabled");
            return;
        }

        _logger.LogInformation("Delay message service starting...");
        
        await _scheduler.StartAsync(stoppingToken);

        // 保持运行直到取消
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }

        await _scheduler.StopAsync(CancellationToken.None);
        
        _logger.LogInformation("Delay message service stopped");
    }
}
