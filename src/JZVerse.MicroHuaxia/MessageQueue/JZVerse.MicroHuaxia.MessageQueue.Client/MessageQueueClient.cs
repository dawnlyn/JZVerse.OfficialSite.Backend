using JZVerse.MicroHuaxia.MessageQueue.Abstractions;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;
using JZVerse.MicroHuaxia.MessageQueue.Protocol.Tcp;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.MessageQueue.Client;

/// <summary>
/// 消息队列客户端
/// </summary>
public sealed class MessageQueueClient : IAsyncDisposable
{
    private readonly ILogger<MessageQueueClient> _logger;
    private readonly MessageQueueClientOptions _options;
    private readonly TcpClientConnection _connection;
    private readonly Timer _heartbeatTimer;

    private bool _disposed;
    private int _reconnectAttempts;

    /// <summary>是否已连接</summary>
    public bool IsConnected => _connection.IsConnected;

    /// <summary>会话ID</summary>
    public string? SessionId => _connection.SessionId;

    /// <summary>消息接收回调</summary>
    public Func<IMessage, Task>? OnMessageReceived { get; set; }

    /// <summary>连接断开回调</summary>
    public Action? OnDisconnected { get; set; }

    /// <summary>重连成功回调</summary>
    public Action? OnReconnected { get; set; }

    public MessageQueueClient(ILogger<MessageQueueClient> logger, MessageQueueClientOptions options)
    {
        _logger = logger;
        _options = options;
        _connection = new TcpClientConnection(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TcpClientConnection>.Instance);

        _connection.OnMessagePushed = HandlePushedMessageAsync;

        // 心跳定时器
        _heartbeatTimer = new Timer(
            SendHeartbeatCallback,
            null,
            Timeout.Infinite,
            Timeout.Infinite);
    }

    /// <summary>
    /// 连接到服务器
    /// </summary>
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new ConnectRequest
            {
                ClientId = _options.ClientId ?? Environment.MachineName,
                ConsumerGroup = _options.ConsumerGroup,
                HeartbeatInterval = _options.HeartbeatInterval
            };

            var response = await _connection.ConnectAsync(
                _options.Host,
                _options.Port,
                request,
                cancellationToken);

            if (response.Success)
            {
                _logger.LogInformation("Connected to {Host}:{Port}, SessionId: {SessionId}",
                    _options.Host, _options.Port, response.SessionId);

                // 启动心跳
                StartHeartbeat();
                _reconnectAttempts = 0;
                return true;
            }

            _logger.LogWarning("Connection failed: {Error}", response.ErrorMessage);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to {Host}:{Port}", _options.Host, _options.Port);
            return false;
        }
    }

    /// <summary>
    /// 发布消息
    /// </summary>
    public async Task<PublishResult> PublishAsync(IMessage message, CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var payload = TcpPayloadSerializer.SerializeMessage(message);
        var response = await _connection.SendAndWaitAsync(
            TcpFrameType.Publish,
            payload,
            cancellationToken,
            _options.RequestTimeout);

        if (response.Header.FrameType == TcpFrameType.Error)
        {
            var error = TcpPayloadSerializer.DeserializeError(response.Body);
            return new PublishResult
            {
                Success = false,
                ErrorMessage = error?.Message
            };
        }

        var publishResponse = TcpPayloadSerializer.DeserializePublishResponse(response.Body);
        return new PublishResult
        {
            Success = publishResponse?.Success ?? false,
            MessageId = publishResponse?.MessageId,
            Offset = publishResponse?.Offset ?? 0,
            Partition = publishResponse?.Partition ?? 0,
            ErrorMessage = publishResponse?.ErrorMessage
        };
    }

    /// <summary>
    /// 批量发布消息
    /// </summary>
    public async Task<PublishResult> PublishBatchAsync(
        IEnumerable<IMessage> messages,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var payload = TcpPayloadSerializer.SerializeMessages(messages);
        var response = await _connection.SendAndWaitAsync(
            TcpFrameType.PublishBatch,
            payload,
            cancellationToken,
            _options.RequestTimeout);

        if (response.Header.FrameType == TcpFrameType.Error)
        {
            var error = TcpPayloadSerializer.DeserializeError(response.Body);
            return new PublishResult
            {
                Success = false,
                ErrorMessage = error?.Message
            };
        }

        var publishResponse = TcpPayloadSerializer.DeserializePublishResponse(response.Body);
        return new PublishResult
        {
            Success = publishResponse?.Success ?? false,
            MessageId = publishResponse?.MessageId,
            Offset = publishResponse?.Offset ?? 0,
            Partition = publishResponse?.Partition ?? 0,
            ErrorMessage = publishResponse?.ErrorMessage
        };
    }

    /// <summary>
    /// 订阅主题
    /// </summary>
    public async Task<bool> SubscribeAsync(
        string topic,
        string? tag = null,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var request = new SubscribeRequest
        {
            Topic = topic,
            Tag = tag,
            ConsumerGroup = _options.ConsumerGroup
        };

        var response = await _connection.SendAndWaitAsync(
            TcpFrameType.Subscribe,
            TcpPayloadSerializer.SerializeSubscribeRequest(request),
            cancellationToken,
            _options.RequestTimeout);

        var success = response.Header.FrameType == TcpFrameType.SubscribeAck;
        if (success)
        {
            _logger.LogInformation("Subscribed to topic {Topic}", topic);
        }

        return success;
    }

    /// <summary>
    /// 取消订阅
    /// </summary>
    public async Task<bool> UnsubscribeAsync(string topic, CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var request = new SubscribeRequest { Topic = topic };

        var response = await _connection.SendAndWaitAsync(
            TcpFrameType.Unsubscribe,
            TcpPayloadSerializer.SerializeSubscribeRequest(request),
            cancellationToken,
            _options.RequestTimeout);

        var success = response.Header.FrameType == TcpFrameType.SubscribeAck;
        if (success)
        {
            _logger.LogInformation("Unsubscribed from topic {Topic}", topic);
        }

        return success;
    }

    /// <summary>
    /// 拉取消息
    /// </summary>
    public async Task<IReadOnlyList<IMessage>> PullAsync(
        string topic,
        int partition = 0,
        long offset = -1,
        int maxCount = 100,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var request = new PullRequest
        {
            Topic = topic,
            Partition = partition,
            Offset = offset,
            MaxCount = maxCount,
            TimeoutMs = _options.RequestTimeout
        };

        var response = await _connection.SendAndWaitAsync(
            TcpFrameType.Pull,
            TcpPayloadSerializer.SerializePullRequest(request),
            cancellationToken,
            _options.RequestTimeout);

        if (response.Header.FrameType == TcpFrameType.PullResponse)
        {
            return TcpPayloadSerializer.DeserializeMessages(response.Body);
        }

        return [];
    }

    /// <summary>
    /// 确认消息
    /// </summary>
    public async Task AcknowledgeAsync(
        string topic,
        int partition,
        long offset,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var request = new AckRequest
        {
            Topic = topic,
            Partition = partition,
            Offset = offset,
            ConsumerGroup = _options.ConsumerGroup
        };

        await _connection.SendAndWaitAsync(
            TcpFrameType.Ack,
            TcpPayloadSerializer.SerializeAckRequest(request),
            cancellationToken,
            _options.RequestTimeout);
    }

    #region 事务消息方法

    /// <summary>
    /// 发送半消息（事务准备阶段）
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>事务结果，包含事务ID</returns>
    public async Task<TransactionPrepareResult> TransactionPrepareAsync(
        IMessage message,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var payload = TcpPayloadSerializer.SerializeMessage(message);
        var response = await _connection.SendAndWaitAsync(
            TcpFrameType.TransactionPrepare,
            payload,
            cancellationToken,
            _options.RequestTimeout);

        if (response.Header.FrameType == TcpFrameType.Error)
        {
            var error = TcpPayloadSerializer.DeserializeError(response.Body);
            return new TransactionPrepareResult
            {
                Success = false,
                ErrorMessage = error?.Message
            };
        }

        var prepareResponse = TcpPayloadSerializer.DeserializeTransactionPrepareResponse(response.Body);
        return new TransactionPrepareResult
        {
            Success = prepareResponse?.Success ?? false,
            TransactionId = prepareResponse?.TransactionId,
            MessageId = prepareResponse?.MessageId,
            ErrorMessage = prepareResponse?.ErrorMessage
        };
    }

    /// <summary>
    /// 提交事务
    /// </summary>
    /// <param name="transactionId">事务ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<TransactionResult> TransactionCommitAsync(
        string transactionId,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var request = new TransactionRequest { TransactionId = transactionId };
        var response = await _connection.SendAndWaitAsync(
            TcpFrameType.TransactionCommit,
            TcpPayloadSerializer.SerializeTransactionRequest(request),
            cancellationToken,
            _options.RequestTimeout);

        if (response.Header.FrameType == TcpFrameType.Error)
        {
            var error = TcpPayloadSerializer.DeserializeError(response.Body);
            return new TransactionResult
            {
                Success = false,
                TransactionId = transactionId,
                ErrorMessage = error?.Message
            };
        }

        var txnResponse = TcpPayloadSerializer.DeserializeTransactionResponse(response.Body);
        return new TransactionResult
        {
            Success = txnResponse?.Success ?? false,
            TransactionId = txnResponse?.TransactionId,
            ErrorMessage = txnResponse?.ErrorMessage
        };
    }

    /// <summary>
    /// 回滚事务
    /// </summary>
    /// <param name="transactionId">事务ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<TransactionResult> TransactionRollbackAsync(
        string transactionId,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var request = new TransactionRequest { TransactionId = transactionId };
        var response = await _connection.SendAndWaitAsync(
            TcpFrameType.TransactionRollback,
            TcpPayloadSerializer.SerializeTransactionRequest(request),
            cancellationToken,
            _options.RequestTimeout);

        if (response.Header.FrameType == TcpFrameType.Error)
        {
            var error = TcpPayloadSerializer.DeserializeError(response.Body);
            return new TransactionResult
            {
                Success = false,
                TransactionId = transactionId,
                ErrorMessage = error?.Message
            };
        }

        var txnResponse = TcpPayloadSerializer.DeserializeTransactionResponse(response.Body);
        return new TransactionResult
        {
            Success = txnResponse?.Success ?? false,
            TransactionId = txnResponse?.TransactionId,
            ErrorMessage = txnResponse?.ErrorMessage
        };
    }

    /// <summary>
    /// 执行事务消息（简化版：自动处理准备和提交/回滚）
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="localTransaction">本地事务执行器，返回 true 表示提交，false 表示回滚</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<TransactionResult> ExecuteTransactionAsync(
        IMessage message,
        Func<string, CancellationToken, Task<bool>> localTransaction,
        CancellationToken cancellationToken = default)
    {
        // Phase 1: Prepare
        var prepareResult = await TransactionPrepareAsync(message, cancellationToken);
        if (!prepareResult.Success || prepareResult.TransactionId is null)
        {
            return new TransactionResult
            {
                Success = false,
                ErrorMessage = prepareResult.ErrorMessage ?? "Failed to prepare transaction"
            };
        }

        var transactionId = prepareResult.TransactionId;

        try
        {
            // Execute local transaction
            var shouldCommit = await localTransaction(transactionId, cancellationToken);

            // Phase 2: Commit or Rollback
            if (shouldCommit)
            {
                return await TransactionCommitAsync(transactionId, cancellationToken);
            }
            else
            {
                var rollbackResult = await TransactionRollbackAsync(transactionId, cancellationToken);
                return new TransactionResult
                {
                    Success = false,
                    TransactionId = transactionId,
                    ErrorMessage = "Local transaction requested rollback"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Local transaction failed for {TransactionId}, rolling back", transactionId);
            await TransactionRollbackAsync(transactionId, cancellationToken);
            return new TransactionResult
            {
                Success = false,
                TransactionId = transactionId,
                ErrorMessage = ex.Message
            };
        }
    }

    #endregion

    /// <summary>
    /// 断开连接
    /// </summary>
    public async Task DisconnectAsync()
    {
        StopHeartbeat();
        await _connection.DisconnectAsync();
        _logger.LogInformation("Disconnected from server");
    }

    private Task HandlePushedMessageAsync(TcpFrame frame)
    {
        if (OnMessageReceived == null)
        {
            return Task.CompletedTask;
        }

        var message = TcpPayloadSerializer.DeserializeMessage(frame.Body);
        if (message != null)
        {
            return OnMessageReceived(message);
        }

        return Task.CompletedTask;
    }

    private void EnsureConnected()
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Not connected to server");
        }
    }

    private void StartHeartbeat()
    {
        var interval = TimeSpan.FromSeconds(_options.HeartbeatInterval);
        _heartbeatTimer.Change(interval, interval);
    }

    private void StopHeartbeat()
    {
        _heartbeatTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private async void SendHeartbeatCallback(object? state)
    {
        try
        {
            if (IsConnected)
            {
                await _connection.SendHeartbeatAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Heartbeat failed");

            if (_options.AutoReconnect)
            {
                await TryReconnectAsync();
            }
        }
    }

    private async Task TryReconnectAsync()
    {
        if (_options.MaxReconnectAttempts > 0 && _reconnectAttempts >= _options.MaxReconnectAttempts)
        {
            _logger.LogWarning("Max reconnect attempts reached");
            OnDisconnected?.Invoke();
            return;
        }

        _reconnectAttempts++;
        _logger.LogInformation("Attempting to reconnect ({Attempt})...", _reconnectAttempts);

        await Task.Delay(_options.ReconnectInterval);

        if (await ConnectAsync())
        {
            OnReconnected?.Invoke();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        StopHeartbeat();
        _heartbeatTimer.Dispose();
        await _connection.DisposeAsync();
    }
}

/// <summary>
/// 发布结果
/// </summary>
public sealed class PublishResult
{
    public bool Success { get; init; }
    public string? MessageId { get; init; }
    public long Offset { get; init; }
    public int Partition { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// 事务准备结果
/// </summary>
public sealed class TransactionPrepareResult
{
    public bool Success { get; init; }
    public string? TransactionId { get; init; }
    public string? MessageId { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// 事务操作结果
/// </summary>
public sealed class TransactionResult
{
    public bool Success { get; init; }
    public string? TransactionId { get; init; }
    public string? ErrorMessage { get; init; }
}
