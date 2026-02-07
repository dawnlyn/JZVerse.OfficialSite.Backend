using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using JZVerse.MicroHuaxia.Gateway.Logging.Storage;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Gateway.Logging.Replay;

/// <summary>
/// WebSocket 日志流提供者
/// </summary>
public sealed class LogStreamProvider : IDisposable
{
    private readonly IStreamableLogStore? _streamableStore;
    private readonly ILogger<LogStreamProvider> _logger;
    private readonly ConcurrentDictionary<string, LogStreamSubscription> _subscriptions = new();
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    /// <summary>
    /// 创建日志流提供者
    /// </summary>
    public LogStreamProvider(
        ILogStore logStore,
        ILogger<LogStreamProvider> logger)
    {
        _streamableStore = logStore as IStreamableLogStore;
        _logger = logger;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        // 订阅日志事件
        if (_streamableStore is not null)
        {
            _streamableStore.LogAdded += OnLogAdded;
        }
    }

    /// <summary>
    /// 创建订阅
    /// </summary>
    public LogStreamSubscription Subscribe(LogQuery? filter = null)
    {
        var subscription = new LogStreamSubscription
        {
            Id = Guid.NewGuid().ToString("N"),
            Filter = filter,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _subscriptions[subscription.Id] = subscription;
        _logger.LogDebug("创建日志流订阅: {SubscriptionId}", subscription.Id);

        return subscription;
    }

    /// <summary>
    /// 取消订阅
    /// </summary>
    public void Unsubscribe(string subscriptionId)
    {
        if (_subscriptions.TryRemove(subscriptionId, out var subscription))
        {
            subscription.CancellationTokenSource.Cancel();
            _logger.LogDebug("取消日志流订阅: {SubscriptionId}", subscriptionId);
        }
    }

    /// <summary>
    /// 处理 WebSocket 连接
    /// </summary>
    public async Task HandleWebSocketAsync(
        WebSocket webSocket,
        LogQuery? filter = null,
        CancellationToken cancellationToken = default)
    {
        var subscription = Subscribe(filter);

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                subscription.CancellationTokenSource.Token);

            // 接收任务
            var receiveTask = ReceiveMessagesAsync(webSocket, subscription, linkedCts.Token);

            // 发送任务
            var sendTask = SendLogsAsync(webSocket, subscription, linkedCts.Token);

            await Task.WhenAny(receiveTask, sendTask);

            linkedCts.Cancel();
        }
        finally
        {
            Unsubscribe(subscription.Id);

            if (webSocket.State == WebSocketState.Open)
            {
                try
                {
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Subscription ended",
                        CancellationToken.None);
                }
                catch { }
            }
        }
    }

    private async Task ReceiveMessagesAsync(
        WebSocket webSocket,
        LogStreamSubscription subscription,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];

        while (!cancellationToken.IsCancellationRequested && webSocket.State == WebSocketState.Open)
        {
            try
            {
                var result = await webSocket.ReceiveAsync(buffer, cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await HandleClientMessageAsync(subscription, message);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (WebSocketException)
            {
                break;
            }
        }
    }

    private async Task SendLogsAsync(
        WebSocket webSocket,
        LogStreamSubscription subscription,
        CancellationToken cancellationToken)
    {
        await foreach (var entry in subscription.LogChannel.Reader.ReadAllAsync(cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (webSocket.State != WebSocketState.Open)
                break;

            try
            {
                var message = new LogStreamMessage
                {
                    Type = "log",
                    Data = entry,
                    Timestamp = DateTimeOffset.UtcNow
                };

                var json = JsonSerializer.Serialize(message, _jsonOptions);
                var bytes = Encoding.UTF8.GetBytes(json);

                await webSocket.SendAsync(
                    bytes,
                    WebSocketMessageType.Text,
                    true,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "发送日志到 WebSocket 失败");
                break;
            }
        }
    }

    private Task HandleClientMessageAsync(LogStreamSubscription subscription, string message)
    {
        try
        {
            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;

            if (root.TryGetProperty("action", out var action))
            {
                var actionValue = action.GetString();

                switch (actionValue)
                {
                    case "updateFilter":
                        if (root.TryGetProperty("filter", out var filterElement))
                        {
                            var newFilter = JsonSerializer.Deserialize<LogQuery>(
                                filterElement.GetRawText(),
                                _jsonOptions);
                            subscription.Filter = newFilter;
                            _logger.LogDebug("更新订阅过滤器: {SubscriptionId}", subscription.Id);
                        }
                        break;

                    case "ping":
                        // 心跳，不需要处理
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析客户端消息失败: {Message}", message);
        }

        return Task.CompletedTask;
    }

    private void OnLogAdded(object? sender, LogEntry entry)
    {
        if (_disposed) return;

        foreach (var subscription in _subscriptions.Values)
        {
            if (MatchesFilter(entry, subscription.Filter))
            {
                subscription.LogChannel.Writer.TryWrite(entry);
            }
        }
    }

    private static bool MatchesFilter(LogEntry entry, LogQuery? filter)
    {
        if (filter is null)
            return true;

        if (filter.MinLevel.HasValue && entry.Level < filter.MinLevel.Value)
            return false;

        if (filter.Levels is { Count: > 0 } && !filter.Levels.Contains(entry.Level))
            return false;

        if (!string.IsNullOrEmpty(filter.ServiceName) && entry.ServiceName != filter.ServiceName)
            return false;

        if (!string.IsNullOrEmpty(filter.TraceId) && entry.TraceId != filter.TraceId)
            return false;

        if (!string.IsNullOrEmpty(filter.SearchText))
        {
            var searchText = filter.SearchText.ToLowerInvariant();
            if (!entry.Message.ToLowerInvariant().Contains(searchText) &&
                (entry.Exception?.ToLowerInvariant().Contains(searchText) != true))
            {
                return false;
            }
        }

        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        if (_streamableStore is not null)
        {
            _streamableStore.LogAdded -= OnLogAdded;
        }

        foreach (var subscription in _subscriptions.Values)
        {
            subscription.CancellationTokenSource.Cancel();
            subscription.LogChannel.Writer.Complete();
        }

        _subscriptions.Clear();
    }
}

/// <summary>
/// 日志流订阅
/// </summary>
public sealed class LogStreamSubscription
{
    /// <summary>
    /// 订阅 ID
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// 过滤条件
    /// </summary>
    public LogQuery? Filter { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// 取消令牌源
    /// </summary>
    public CancellationTokenSource CancellationTokenSource { get; } = new();

    /// <summary>
    /// 日志通道
    /// </summary>
    public System.Threading.Channels.Channel<LogEntry> LogChannel { get; } =
        System.Threading.Channels.Channel.CreateBounded<LogEntry>(
            new System.Threading.Channels.BoundedChannelOptions(1000)
            {
                FullMode = System.Threading.Channels.BoundedChannelFullMode.DropOldest
            });
}

/// <summary>
/// 日志流消息
/// </summary>
public sealed class LogStreamMessage
{
    /// <summary>
    /// 消息类型
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// 数据
    /// </summary>
    public object? Data { get; init; }

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }
}
