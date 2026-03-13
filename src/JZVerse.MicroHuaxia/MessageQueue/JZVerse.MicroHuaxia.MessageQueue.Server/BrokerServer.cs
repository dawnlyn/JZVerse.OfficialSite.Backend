using System.Collections.Concurrent;
using System.Net;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Storage;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Transaction;
using JZVerse.MicroHuaxia.MessageQueue.Core.Routing;
using JZVerse.MicroHuaxia.MessageQueue.Core.Subscription;
using JZVerse.MicroHuaxia.MessageQueue.Protocol.Tcp;
using JZVerse.MicroHuaxia.MessageQueue.Server.Management;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.MessageQueue.Server;

/// <summary>
/// 消息队列 Broker 服务器
/// </summary>
public sealed class BrokerServer : IHostedService, IAsyncDisposable, IBrokerManagement
{
    private readonly ILogger<BrokerServer> _logger;
    private readonly BrokerOptions _options;
    private readonly TcpServerListener _listener;
    private readonly IMessageStore _messageStore;
    private readonly IOffsetManager _offsetManager;
    private readonly RoutingEngine _routingEngine;
    private readonly SubscriptionManager _subscriptionManager;
    private readonly ExchangeManager _exchangeManager;
    private readonly QueueManager _queueManager;
    private readonly ITransactionProducer? _transactionProducer;

    // 客户端会话管理
    private readonly ConcurrentDictionary<string, ClientSession> _clientSessions = new();

    // 主题订阅映射: topic -> [sessionId]
    private readonly ConcurrentDictionary<string, HashSet<string>> _topicSubscriptions = new();

    public BrokerServer(
        ILogger<BrokerServer> logger,
        BrokerOptions options,
        TcpServerListener listener,
        IMessageStore messageStore,
        IOffsetManager offsetManager,
        RoutingEngine routingEngine,
        SubscriptionManager subscriptionManager,
        ExchangeManager exchangeManager,
        QueueManager queueManager,
        ITransactionProducer? transactionProducer = null)
    {
        _logger = logger;
        _options = options;
        _listener = listener;
        _messageStore = messageStore;
        _offsetManager = offsetManager;
        _routingEngine = routingEngine;
        _subscriptionManager = subscriptionManager;
        _exchangeManager = exchangeManager;
        _queueManager = queueManager;
        _transactionProducer = transactionProducer;

        // 配置回调
        _listener.OnClientConnected = HandleClientConnectedAsync;
        _listener.OnClientDisconnected = HandleClientDisconnectedAsync;
        _listener.OnFrameReceived = HandleFrameReceivedAsync;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var address = IPAddress.Parse(_options.Host);
        _listener.Start(address, _options.Port);

        _logger.LogInformation("Broker server started on {Host}:{Port}", _options.Host, _options.Port);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping broker server...");
        await _listener.StopAsync();
        _logger.LogInformation("Broker server stopped");
    }

    private Task<ConnectResponse> HandleClientConnectedAsync(TcpServerSession session, ConnectRequest request)
    {
        _logger.LogInformation("Client connecting: {ClientId}, ConsumerGroup: {ConsumerGroup}",
            request.ClientId, request.ConsumerGroup);

        // 创建客户端会话
        var clientSession = new ClientSession
        {
            SessionId = session.SessionId,
            ClientId = request.ClientId ?? session.SessionId,
            ConsumerGroup = request.ConsumerGroup,
            ConnectedAt = DateTimeOffset.UtcNow,
            LastHeartbeat = DateTimeOffset.UtcNow
        };

        _clientSessions[session.SessionId] = clientSession;

        return Task.FromResult(new ConnectResponse
        {
            Success = true,
            SessionId = session.SessionId,
            ServerVersion = TcpFrameHeader.CurrentVersion
        });
    }

    private Task HandleClientDisconnectedAsync(TcpServerSession session)
    {
        _logger.LogInformation("Client disconnected: {SessionId}", session.SessionId);

        if (_clientSessions.TryRemove(session.SessionId, out var clientSession))
        {
            // 清理订阅
            foreach (var topic in clientSession.SubscribedTopics)
            {
                if (_topicSubscriptions.TryGetValue(topic, out var sessions))
                {
                    lock (sessions)
                    {
                        sessions.Remove(session.SessionId);
                    }
                }
            }
        }

        return Task.CompletedTask;
    }

    private async Task<TcpFrame?> HandleFrameReceivedAsync(TcpServerSession session, TcpFrame frame)
    {
        try
        {
            return frame.Header.FrameType switch
            {
                TcpFrameType.Publish => await HandlePublishAsync(session, frame),
                TcpFrameType.PublishBatch => await HandlePublishBatchAsync(session, frame),
                TcpFrameType.Subscribe => await HandleSubscribeAsync(session, frame),
                TcpFrameType.Unsubscribe => await HandleUnsubscribeAsync(session, frame),
                TcpFrameType.Pull => await HandlePullAsync(session, frame),
                TcpFrameType.Ack => await HandleAckAsync(session, frame),
                TcpFrameType.Nack => await HandleNackAsync(session, frame),
                TcpFrameType.TransactionPrepare => await HandleTransactionPrepareAsync(session, frame),
                TcpFrameType.TransactionCommit => await HandleTransactionCommitAsync(session, frame),
                TcpFrameType.TransactionRollback => await HandleTransactionRollbackAsync(session, frame),
                _ => CreateErrorFrame(frame.Header.RequestId, 400, "Unknown frame type")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling frame type {FrameType}", frame.Header.FrameType);
            return CreateErrorFrame(frame.Header.RequestId, 500, ex.Message);
        }
    }

    private async Task<TcpFrame> HandlePublishAsync(TcpServerSession session, TcpFrame frame)
    {
        var message = TcpPayloadSerializer.DeserializeMessage(frame.Body);
        if (message == null)
        {
            return CreateErrorFrame(frame.Header.RequestId, 400, "Invalid message payload");
        }

        // 路由到分区
        var partition = CalculatePartition(message, _options.DefaultPartitions);

        // 存储消息
        var offset = await _messageStore.AppendAsync(message, partition);

        _logger.LogDebug("Message published: {MessageId} -> {Topic}:{Partition}@{Offset}",
            message.MessageId, message.Topic, partition, offset);

        // 推送给订阅者
        await PushToSubscribersAsync(message.Topic, message);

        // 返回响应
        var response = new PublishResponse
        {
            Success = true,
            MessageId = message.MessageId,
            Offset = offset,
            Partition = partition
        };

        return new TcpFrame(
            TcpFrameType.PublishAck,
            TcpPayloadSerializer.SerializePublishResponse(response),
            frame.Header.RequestId);
    }

    private async Task<TcpFrame> HandlePublishBatchAsync(TcpServerSession session, TcpFrame frame)
    {
        var messages = TcpPayloadSerializer.DeserializeMessages(frame.Body);
        if (messages.Count == 0)
        {
            return CreateErrorFrame(frame.Header.RequestId, 400, "Empty message batch");
        }

        var firstMessage = messages[0];
        var partition = CalculatePartition(firstMessage, _options.DefaultPartitions);

        var firstOffset = await _messageStore.AppendBatchAsync(messages, partition);

        _logger.LogDebug("Batch published: {Count} messages -> {Topic}:{Partition}@{Offset}",
            messages.Count, firstMessage.Topic, partition, firstOffset);

        // 推送给订阅者
        foreach (var message in messages)
        {
            await PushToSubscribersAsync(message.Topic, message);
        }

        var response = new PublishResponse
        {
            Success = true,
            MessageId = firstMessage.MessageId,
            Offset = firstOffset,
            Partition = partition
        };

        return new TcpFrame(
            TcpFrameType.PublishAck,
            TcpPayloadSerializer.SerializePublishResponse(response),
            frame.Header.RequestId);
    }

    private Task<TcpFrame> HandleSubscribeAsync(TcpServerSession session, TcpFrame frame)
    {
        var request = TcpPayloadSerializer.DeserializeSubscribeRequest(frame.Body);
        if (request?.Topic == null)
        {
            return Task.FromResult(CreateErrorFrame(frame.Header.RequestId, 400, "Invalid subscribe request"));
        }

        // 记录订阅
        if (_clientSessions.TryGetValue(session.SessionId, out var clientSession))
        {
            clientSession.SubscribedTopics.Add(request.Topic);

            if (!string.IsNullOrEmpty(request.ConsumerGroup))
            {
                clientSession.ConsumerGroup = request.ConsumerGroup;
            }
        }

        // 添加到主题订阅映射
        var sessions = _topicSubscriptions.GetOrAdd(request.Topic, _ => new HashSet<string>());
        lock (sessions)
        {
            sessions.Add(session.SessionId);
        }

        _logger.LogInformation("Client {SessionId} subscribed to {Topic}", session.SessionId, request.Topic);

        return Task.FromResult(new TcpFrame(
            TcpFrameType.SubscribeAck,
            [],
            frame.Header.RequestId));
    }

    private Task<TcpFrame> HandleUnsubscribeAsync(TcpServerSession session, TcpFrame frame)
    {
        var request = TcpPayloadSerializer.DeserializeSubscribeRequest(frame.Body);
        if (request?.Topic == null)
        {
            return Task.FromResult(CreateErrorFrame(frame.Header.RequestId, 400, "Invalid unsubscribe request"));
        }

        // 移除订阅
        if (_clientSessions.TryGetValue(session.SessionId, out var clientSession))
        {
            clientSession.SubscribedTopics.Remove(request.Topic);
        }

        if (_topicSubscriptions.TryGetValue(request.Topic, out var sessions))
        {
            lock (sessions)
            {
                sessions.Remove(session.SessionId);
            }
        }

        _logger.LogInformation("Client {SessionId} unsubscribed from {Topic}", session.SessionId, request.Topic);

        return Task.FromResult(new TcpFrame(
            TcpFrameType.SubscribeAck,
            [],
            frame.Header.RequestId));
    }

    private async Task<TcpFrame> HandlePullAsync(TcpServerSession session, TcpFrame frame)
    {
        var request = TcpPayloadSerializer.DeserializePullRequest(frame.Body);
        if (request?.Topic == null)
        {
            return CreateErrorFrame(frame.Header.RequestId, 400, "Invalid pull request");
        }

        // 获取消费组
        string consumerGroup = "default";
        if (_clientSessions.TryGetValue(session.SessionId, out var clientSession))
        {
            consumerGroup = clientSession.ConsumerGroup ?? consumerGroup;
        }

        // 如果没有指定 offset，从 offset manager 获取
        var offset = request.Offset;
        if (offset < 0)
        {
            offset = await _offsetManager.GetOffsetAsync(consumerGroup, request.Topic, request.Partition);
            if (offset < 0)
            {
                offset = 0;
            }
        }

        // 获取消息
        var messages = await _messageStore.GetByOffsetAsync(
            request.Topic,
            request.Partition,
            offset,
            request.MaxCount);

        _logger.LogDebug("Pull {Count} messages from {Topic}:{Partition}@{Offset}",
            messages.Count, request.Topic, request.Partition, offset);

        return new TcpFrame(
            TcpFrameType.PullResponse,
            TcpPayloadSerializer.SerializeMessages(messages),
            frame.Header.RequestId);
    }

    private async Task<TcpFrame> HandleAckAsync(TcpServerSession session, TcpFrame frame)
    {
        var request = TcpPayloadSerializer.DeserializeAckRequest(frame.Body);
        if (request?.Topic == null)
        {
            return CreateErrorFrame(frame.Header.RequestId, 400, "Invalid ack request");
        }

        var consumerGroup = request.ConsumerGroup ?? "default";
        await _offsetManager.CommitOffsetAsync(consumerGroup, request.Topic, request.Partition, request.Offset);

        _logger.LogDebug("Offset committed: [{ConsumerGroup}] {Topic}:{Partition} -> {Offset}",
            consumerGroup, request.Topic, request.Partition, request.Offset);

        return new TcpFrame(TcpFrameType.Ack, [], frame.Header.RequestId);
    }

    private Task<TcpFrame> HandleNackAsync(TcpServerSession session, TcpFrame frame)
    {
        var request = TcpPayloadSerializer.DeserializeAckRequest(frame.Body);
        if (request != null)
        {
            _logger.LogWarning("Message nacked: {MessageId}", request.MessageId);
            // 可以实现重新投递逻辑
        }

        return Task.FromResult(new TcpFrame(TcpFrameType.Nack, [], frame.Header.RequestId));
    }

    private async Task<TcpFrame> HandleTransactionPrepareAsync(TcpServerSession session, TcpFrame frame)
    {
        if (_transactionProducer is null)
        {
            return CreateErrorFrame(frame.Header.RequestId, 501, "Transaction support not enabled");
        }

        var message = TcpPayloadSerializer.DeserializeMessage(frame.Body);
        if (message is null)
        {
            return CreateErrorFrame(frame.Header.RequestId, 400, "Invalid message payload");
        }

        try
        {
            var result = await _transactionProducer.PrepareAsync(message);
            
            var response = new TransactionPrepareResponse
            {
                Success = result.Success,
                TransactionId = result.TransactionId,
                MessageId = result.MessageId,
                ErrorMessage = result.Error
            };

            _logger.LogDebug("Transaction prepared: {TransactionId}, MessageId: {MessageId}",
                result.TransactionId, result.MessageId);

            return new TcpFrame(
                TcpFrameType.TransactionPrepareAck,
                TcpPayloadSerializer.SerializeTransactionPrepareResponse(response),
                frame.Header.RequestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to prepare transaction for message {MessageId}", message.MessageId);
            return CreateErrorFrame(frame.Header.RequestId, 500, ex.Message);
        }
    }

    private async Task<TcpFrame> HandleTransactionCommitAsync(TcpServerSession session, TcpFrame frame)
    {
        if (_transactionProducer is null)
        {
            return CreateErrorFrame(frame.Header.RequestId, 501, "Transaction support not enabled");
        }

        var request = TcpPayloadSerializer.DeserializeTransactionRequest(frame.Body);
        if (request?.TransactionId is null)
        {
            return CreateErrorFrame(frame.Header.RequestId, 400, "Invalid transaction request");
        }

        try
        {
            await _transactionProducer.CommitAsync(request.TransactionId);

            var response = new TransactionResponse
            {
                Success = true,
                TransactionId = request.TransactionId
            };

            _logger.LogInformation("Transaction committed: {TransactionId}", request.TransactionId);

            return new TcpFrame(
                TcpFrameType.TransactionCommitAck,
                TcpPayloadSerializer.SerializeTransactionResponse(response),
                frame.Header.RequestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to commit transaction {TransactionId}", request.TransactionId);
            
            var response = new TransactionResponse
            {
                Success = false,
                TransactionId = request.TransactionId,
                ErrorMessage = ex.Message
            };

            return new TcpFrame(
                TcpFrameType.TransactionCommitAck,
                TcpPayloadSerializer.SerializeTransactionResponse(response),
                frame.Header.RequestId);
        }
    }

    private async Task<TcpFrame> HandleTransactionRollbackAsync(TcpServerSession session, TcpFrame frame)
    {
        if (_transactionProducer is null)
        {
            return CreateErrorFrame(frame.Header.RequestId, 501, "Transaction support not enabled");
        }

        var request = TcpPayloadSerializer.DeserializeTransactionRequest(frame.Body);
        if (request?.TransactionId is null)
        {
            return CreateErrorFrame(frame.Header.RequestId, 400, "Invalid transaction request");
        }

        try
        {
            await _transactionProducer.RollbackAsync(request.TransactionId);

            var response = new TransactionResponse
            {
                Success = true,
                TransactionId = request.TransactionId
            };

            _logger.LogInformation("Transaction rolled back: {TransactionId}", request.TransactionId);

            return new TcpFrame(
                TcpFrameType.TransactionRollbackAck,
                TcpPayloadSerializer.SerializeTransactionResponse(response),
                frame.Header.RequestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rollback transaction {TransactionId}", request.TransactionId);
            
            var response = new TransactionResponse
            {
                Success = false,
                TransactionId = request.TransactionId,
                ErrorMessage = ex.Message
            };

            return new TcpFrame(
                TcpFrameType.TransactionRollbackAck,
                TcpPayloadSerializer.SerializeTransactionResponse(response),
                frame.Header.RequestId);
        }
    }

    private async Task PushToSubscribersAsync(string topic, IMessage message)
    {
        if (!_topicSubscriptions.TryGetValue(topic, out var sessions))
        {
            return;
        }

        List<string> sessionIds;
        lock (sessions)
        {
            sessionIds = sessions.ToList();
        }

        var payload = TcpPayloadSerializer.SerializeMessage(message);
        var pushFrame = new TcpFrame(TcpFrameType.Push, payload);

        foreach (var sessionId in sessionIds)
        {
            try
            {
                await _listener.PushToSessionAsync(sessionId, pushFrame);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to push message to session {SessionId}", sessionId);
            }
        }
    }

    private static TcpFrame CreateErrorFrame(uint requestId, int code, string message)
    {
        var error = new ErrorResponse { Code = code, Message = message };
        return new TcpFrame(
            TcpFrameType.Error,
            TcpPayloadSerializer.SerializeError(error),
            requestId);
    }

    private static int CalculatePartition(IMessage message, int partitionCount)
    {
        if (!string.IsNullOrEmpty(message.PartitionKey))
        {
            return Math.Abs(message.PartitionKey.GetHashCode()) % partitionCount;
        }
        return Random.Shared.Next(partitionCount);
    }

    public async ValueTask DisposeAsync()
    {
        await _listener.DisposeAsync();
    }

    // ==================== IBrokerManagement ====================

    /// <inheritdoc />
    public async Task<BrokerStats> GetStatsAsync(CancellationToken cancellationToken)
    {
        var topics = await GetTopicsAsync(cancellationToken);
        return new BrokerStats
        {
            TotalTopics = topics.Count,
            TotalMessages = topics.Sum(t => t.TotalMessages),
            ConnectedClients = _clientSessions.Count,
            PendingMessages = 0
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TopicInfo>> GetTopicsAsync(CancellationToken cancellationToken)
    {
        var result = new List<TopicInfo>();
        var allTopics = GetAllKnownTopics();

        foreach (var topic in allTopics)
        {
            long totalMessages = 0;
            int partitionCount = 0;

            for (int p = 0; p < _options.DefaultPartitions; p++)
            {
                var latest = await _messageStore.GetLatestOffsetAsync(topic, p, cancellationToken);
                var earliest = await _messageStore.GetEarliestOffsetAsync(topic, p, cancellationToken);
                if (latest > 0 || earliest > 0)
                {
                    partitionCount++;
                    totalMessages += latest - earliest;
                }
            }

            int subscriberCount = 0;
            if (_topicSubscriptions.TryGetValue(topic, out var sessions))
            {
                lock (sessions)
                {
                    subscriberCount = sessions.Count;
                }
            }

            if (partitionCount > 0 || subscriberCount > 0)
            {
                result.Add(new TopicInfo
                {
                    Name = topic,
                    PartitionCount = partitionCount > 0 ? partitionCount : _options.DefaultPartitions,
                    TotalMessages = totalMessages,
                    SubscriberCount = subscriberCount
                });
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<TopicDetail?> GetTopicDetailAsync(string topic, CancellationToken cancellationToken)
    {
        var partitions = new List<Management.PartitionInfo>();
        bool hasData = false;

        for (int p = 0; p < _options.DefaultPartitions; p++)
        {
            var latest = await _messageStore.GetLatestOffsetAsync(topic, p, cancellationToken);
            var earliest = await _messageStore.GetEarliestOffsetAsync(topic, p, cancellationToken);
            if (latest > 0 || earliest > 0) hasData = true;

            partitions.Add(new Management.PartitionInfo
            {
                PartitionId = p,
                LatestOffset = latest,
                EarliestOffset = earliest,
                MessageCount = latest - earliest
            });
        }

        var subscribers = new List<string>();
        if (_topicSubscriptions.TryGetValue(topic, out var sessions))
        {
            lock (sessions)
            {
                foreach (var sessionId in sessions)
                {
                    if (_clientSessions.TryGetValue(sessionId, out var cs))
                    {
                        subscribers.Add(cs.ClientId);
                    }
                }
            }
        }

        if (!hasData && subscribers.Count == 0)
        {
            return null;
        }

        return new TopicDetail
        {
            Name = topic,
            Partitions = partitions,
            Subscribers = subscribers
        };
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ConsumerGroupInfo>> GetConsumerGroupsAsync(CancellationToken cancellationToken)
    {
        var groups = _clientSessions.Values
            .Where(s => !string.IsNullOrEmpty(s.ConsumerGroup))
            .GroupBy(s => s.ConsumerGroup!)
            .Select(g => new ConsumerGroupInfo
            {
                GroupName = g.Key,
                SubscribedTopics = g.SelectMany(s => s.SubscribedTopics).Distinct().ToList(),
                TotalLag = 0
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<ConsumerGroupInfo>>(groups);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ClientConnectionInfo>> GetConnectionsAsync(CancellationToken cancellationToken)
    {
        var connections = _clientSessions.Values
            .Select(s => new ClientConnectionInfo
            {
                ClientId = s.ClientId,
                SessionId = s.SessionId,
                ConnectedAt = s.ConnectedAt,
                Topics = s.SubscribedTopics.ToList()
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<ClientConnectionInfo>>(connections);
    }

    private HashSet<string> GetAllKnownTopics()
    {
        var topics = new HashSet<string>();
        foreach (var topic in _topicSubscriptions.Keys)
        {
            topics.Add(topic);
        }
        foreach (var session in _clientSessions.Values)
        {
            foreach (var topic in session.SubscribedTopics)
            {
                topics.Add(topic);
            }
        }
        return topics;
    }
}

/// <summary>
/// 客户端会话信息
/// </summary>
internal sealed class ClientSession
{
    public string SessionId { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string? ConsumerGroup { get; set; }
    public DateTimeOffset ConnectedAt { get; init; }
    public DateTimeOffset LastHeartbeat { get; set; }
    public HashSet<string> SubscribedTopics { get; } = new();
}
