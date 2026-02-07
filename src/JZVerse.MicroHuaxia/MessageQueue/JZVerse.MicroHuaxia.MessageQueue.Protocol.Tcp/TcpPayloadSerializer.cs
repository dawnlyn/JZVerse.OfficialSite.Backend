using System.Text.Json;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;

namespace JZVerse.MicroHuaxia.MessageQueue.Protocol.Tcp;

/// <summary>
/// TCP 消息载荷序列化器
/// </summary>
public static class TcpPayloadSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// 序列化连接请求
    /// </summary>
    public static byte[] SerializeConnectRequest(ConnectRequest request)
        => JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions);

    /// <summary>
    /// 反序列化连接请求
    /// </summary>
    public static ConnectRequest? DeserializeConnectRequest(ReadOnlySpan<byte> data)
        => JsonSerializer.Deserialize<ConnectRequest>(data, JsonOptions);

    /// <summary>
    /// 序列化连接响应
    /// </summary>
    public static byte[] SerializeConnectResponse(ConnectResponse response)
        => JsonSerializer.SerializeToUtf8Bytes(response, JsonOptions);

    /// <summary>
    /// 反序列化连接响应
    /// </summary>
    public static ConnectResponse? DeserializeConnectResponse(ReadOnlySpan<byte> data)
        => JsonSerializer.Deserialize<ConnectResponse>(data, JsonOptions);

    /// <summary>
    /// 序列化消息
    /// </summary>
    public static byte[] SerializeMessage(IMessage message)
    {
        var dto = new MessageDto
        {
            MessageId = message.MessageId,
            Topic = message.Topic,
            Tag = message.Tag,
            Body = Convert.ToBase64String(message.Body),
            Headers = message.Headers.ToDictionary(k => k.Key, v => v.Value),
            Timestamp = message.Timestamp.ToUnixTimeMilliseconds(),
            PartitionKey = message.PartitionKey,
            DelaySeconds = message.DelaySeconds,
            ExpireSeconds = message.ExpireSeconds,
            TransactionId = message.TransactionId,
            Priority = message.Priority
        };
        return JsonSerializer.SerializeToUtf8Bytes(dto, JsonOptions);
    }

    /// <summary>
    /// 反序列化消息
    /// </summary>
    public static IMessage? DeserializeMessage(ReadOnlySpan<byte> data)
    {
        var dto = JsonSerializer.Deserialize<MessageDto>(data, JsonOptions);
        if (dto == null) return null;

        return new Message
        {
            MessageId = dto.MessageId ?? string.Empty,
            Topic = dto.Topic ?? string.Empty,
            Tag = dto.Tag,
            Body = string.IsNullOrEmpty(dto.Body) ? [] : Convert.FromBase64String(dto.Body),
            Headers = dto.Headers ?? new Dictionary<string, string>(),
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(dto.Timestamp),
            PartitionKey = dto.PartitionKey,
            DelaySeconds = dto.DelaySeconds,
            ExpireSeconds = dto.ExpireSeconds,
            TransactionId = dto.TransactionId,
            Priority = dto.Priority
        };
    }

    /// <summary>
    /// 序列化批量消息
    /// </summary>
    public static byte[] SerializeMessages(IEnumerable<IMessage> messages)
    {
        var dtos = messages.Select(m => new MessageDto
        {
            MessageId = m.MessageId,
            Topic = m.Topic,
            Tag = m.Tag,
            Body = Convert.ToBase64String(m.Body),
            Headers = m.Headers.ToDictionary(k => k.Key, v => v.Value),
            Timestamp = m.Timestamp.ToUnixTimeMilliseconds(),
            PartitionKey = m.PartitionKey,
            DelaySeconds = m.DelaySeconds,
            ExpireSeconds = m.ExpireSeconds,
            TransactionId = m.TransactionId,
            Priority = m.Priority
        }).ToList();
        return JsonSerializer.SerializeToUtf8Bytes(dtos, JsonOptions);
    }

    /// <summary>
    /// 反序列化批量消息
    /// </summary>
    public static IReadOnlyList<IMessage> DeserializeMessages(ReadOnlySpan<byte> data)
    {
        var dtos = JsonSerializer.Deserialize<List<MessageDto>>(data, JsonOptions);
        if (dtos == null) return [];

        return dtos.Select(dto => (IMessage)new Message
        {
            MessageId = dto.MessageId ?? string.Empty,
            Topic = dto.Topic ?? string.Empty,
            Tag = dto.Tag,
            Body = string.IsNullOrEmpty(dto.Body) ? [] : Convert.FromBase64String(dto.Body),
            Headers = dto.Headers ?? new Dictionary<string, string>(),
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(dto.Timestamp),
            PartitionKey = dto.PartitionKey,
            DelaySeconds = dto.DelaySeconds,
            ExpireSeconds = dto.ExpireSeconds,
            TransactionId = dto.TransactionId,
            Priority = dto.Priority
        }).ToList();
    }

    /// <summary>
    /// 序列化订阅请求
    /// </summary>
    public static byte[] SerializeSubscribeRequest(SubscribeRequest request)
        => JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions);

    /// <summary>
    /// 反序列化订阅请求
    /// </summary>
    public static SubscribeRequest? DeserializeSubscribeRequest(ReadOnlySpan<byte> data)
        => JsonSerializer.Deserialize<SubscribeRequest>(data, JsonOptions);

    /// <summary>
    /// 序列化拉取请求
    /// </summary>
    public static byte[] SerializePullRequest(PullRequest request)
        => JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions);

    /// <summary>
    /// 反序列化拉取请求
    /// </summary>
    public static PullRequest? DeserializePullRequest(ReadOnlySpan<byte> data)
        => JsonSerializer.Deserialize<PullRequest>(data, JsonOptions);

    /// <summary>
    /// 序列化确认请求
    /// </summary>
    public static byte[] SerializeAckRequest(AckRequest request)
        => JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions);

    /// <summary>
    /// 反序列化确认请求
    /// </summary>
    public static AckRequest? DeserializeAckRequest(ReadOnlySpan<byte> data)
        => JsonSerializer.Deserialize<AckRequest>(data, JsonOptions);

    /// <summary>
    /// 序列化错误响应
    /// </summary>
    public static byte[] SerializeError(ErrorResponse error)
        => JsonSerializer.SerializeToUtf8Bytes(error, JsonOptions);

    /// <summary>
    /// 反序列化错误响应
    /// </summary>
    public static ErrorResponse? DeserializeError(ReadOnlySpan<byte> data)
        => JsonSerializer.Deserialize<ErrorResponse>(data, JsonOptions);

    /// <summary>
    /// 序列化发布响应
    /// </summary>
    public static byte[] SerializePublishResponse(PublishResponse response)
        => JsonSerializer.SerializeToUtf8Bytes(response, JsonOptions);

    /// <summary>
    /// 反序列化发布响应
    /// </summary>
    public static PublishResponse? DeserializePublishResponse(ReadOnlySpan<byte> data)
        => JsonSerializer.Deserialize<PublishResponse>(data, JsonOptions);

    /// <summary>
    /// 序列化事务准备请求（包含消息）
    /// </summary>
    public static byte[] SerializeTransactionPrepareRequest(IMessage message)
        => SerializeMessage(message);

    /// <summary>
    /// 反序列化事务准备请求
    /// </summary>
    public static IMessage? DeserializeTransactionPrepareRequest(ReadOnlySpan<byte> data)
        => DeserializeMessage(data);

    /// <summary>
    /// 序列化事务准备响应
    /// </summary>
    public static byte[] SerializeTransactionPrepareResponse(TransactionPrepareResponse response)
        => JsonSerializer.SerializeToUtf8Bytes(response, JsonOptions);

    /// <summary>
    /// 反序列化事务准备响应
    /// </summary>
    public static TransactionPrepareResponse? DeserializeTransactionPrepareResponse(ReadOnlySpan<byte> data)
        => JsonSerializer.Deserialize<TransactionPrepareResponse>(data, JsonOptions);

    /// <summary>
    /// 序列化事务提交/回滚请求
    /// </summary>
    public static byte[] SerializeTransactionRequest(TransactionRequest request)
        => JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions);

    /// <summary>
    /// 反序列化事务提交/回滚请求
    /// </summary>
    public static TransactionRequest? DeserializeTransactionRequest(ReadOnlySpan<byte> data)
        => JsonSerializer.Deserialize<TransactionRequest>(data, JsonOptions);

    /// <summary>
    /// 序列化事务响应
    /// </summary>
    public static byte[] SerializeTransactionResponse(TransactionResponse response)
        => JsonSerializer.SerializeToUtf8Bytes(response, JsonOptions);

    /// <summary>
    /// 反序列化事务响应
    /// </summary>
    public static TransactionResponse? DeserializeTransactionResponse(ReadOnlySpan<byte> data)
        => JsonSerializer.Deserialize<TransactionResponse>(data, JsonOptions);
}

#region 载荷 DTO

/// <summary>连接请求</summary>
public sealed class ConnectRequest
{
    public string? ClientId { get; set; }
    public string? ConsumerGroup { get; set; }
    public int HeartbeatInterval { get; set; } = 30;
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>连接响应</summary>
public sealed class ConnectResponse
{
    public bool Success { get; set; }
    public string? SessionId { get; set; }
    public string? ErrorMessage { get; set; }
    public int ServerVersion { get; set; }
}

/// <summary>消息 DTO</summary>
internal sealed class MessageDto
{
    public string? MessageId { get; set; }
    public string? Topic { get; set; }
    public string? Tag { get; set; }
    public string? Body { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
    public long Timestamp { get; set; }
    public string? PartitionKey { get; set; }
    public int? DelaySeconds { get; set; }
    public int? ExpireSeconds { get; set; }
    public string? TransactionId { get; set; }
    public int Priority { get; set; }
}

/// <summary>订阅请求</summary>
public sealed class SubscribeRequest
{
    public string? Topic { get; set; }
    public string? Tag { get; set; }
    public string? ConsumerGroup { get; set; }
    public bool FromBeginning { get; set; }
}

/// <summary>拉取请求</summary>
public sealed class PullRequest
{
    public string? Topic { get; set; }
    public int Partition { get; set; }
    public long Offset { get; set; }
    public int MaxCount { get; set; } = 100;
    public int TimeoutMs { get; set; } = 5000;
}

/// <summary>确认请求</summary>
public sealed class AckRequest
{
    public string? Topic { get; set; }
    public int Partition { get; set; }
    public string? ConsumerGroup { get; set; }
    public long Offset { get; set; }
    public string? MessageId { get; set; }
}

/// <summary>错误响应</summary>
public sealed class ErrorResponse
{
    public int Code { get; set; }
    public string? Message { get; set; }
    public string? Details { get; set; }
}

/// <summary>发布响应</summary>
public sealed class PublishResponse
{
    public bool Success { get; set; }
    public string? MessageId { get; set; }
    public long Offset { get; set; }
    public int Partition { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>事务准备响应</summary>
public sealed class TransactionPrepareResponse
{
    public bool Success { get; set; }
    public string? TransactionId { get; set; }
    public string? MessageId { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>事务提交/回滚请求</summary>
public sealed class TransactionRequest
{
    public string? TransactionId { get; set; }
}

/// <summary>事务响应</summary>
public sealed class TransactionResponse
{
    public bool Success { get; set; }
    public string? TransactionId { get; set; }
    public string? ErrorMessage { get; set; }
}

#endregion
