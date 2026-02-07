using System.Text;
using System.Text.Json;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;

namespace JZVerse.MicroHuaxia.MessageQueue.Abstractions;

/// <summary>
/// 消息构建器
/// </summary>
public sealed class MessageBuilder
{
    private string? _topic;
    private string? _tag;
    private byte[]? _body;
    private readonly Dictionary<string, string> _headers = new();
    private string? _partitionKey;
    private int? _delaySeconds;
    private int? _expireSeconds;
    private string? _transactionId;
    private int _priority = 5;

    private MessageBuilder()
    {
    }

    /// <summary>
    /// 创建消息构建器
    /// </summary>
    public static MessageBuilder Create() => new();

    /// <summary>
    /// 设置主题
    /// </summary>
    public MessageBuilder Topic(string topic)
    {
        _topic = topic;
        return this;
    }

    /// <summary>
    /// 设置标签
    /// </summary>
    public MessageBuilder Tag(string tag)
    {
        _tag = tag;
        return this;
    }

    /// <summary>
    /// 设置消息体（字节数组）
    /// </summary>
    public MessageBuilder Body(byte[] body)
    {
        _body = body;
        return this;
    }

    /// <summary>
    /// 设置消息体（字符串）
    /// </summary>
    public MessageBuilder Body(string body)
    {
        _body = Encoding.UTF8.GetBytes(body);
        return this;
    }

    /// <summary>
    /// 设置消息体（对象，JSON 序列化）
    /// </summary>
    public MessageBuilder Body<T>(T body, JsonSerializerOptions? options = null)
    {
        _body = JsonSerializer.SerializeToUtf8Bytes(body, options);
        return this;
    }

    /// <summary>
    /// 添加消息头
    /// </summary>
    public MessageBuilder Header(string key, string value)
    {
        _headers[key] = value;
        return this;
    }

    /// <summary>
    /// 添加多个消息头
    /// </summary>
    public MessageBuilder Headers(IDictionary<string, string> headers)
    {
        foreach (var (key, value) in headers)
        {
            _headers[key] = value;
        }
        return this;
    }

    /// <summary>
    /// 设置分区键
    /// </summary>
    public MessageBuilder PartitionKey(string key)
    {
        _partitionKey = key;
        return this;
    }

    /// <summary>
    /// 设置延迟投递（秒）
    /// </summary>
    public MessageBuilder Delay(int seconds)
    {
        _delaySeconds = seconds;
        return this;
    }

    /// <summary>
    /// 设置延迟投递
    /// </summary>
    public MessageBuilder Delay(TimeSpan delay)
    {
        _delaySeconds = (int)delay.TotalSeconds;
        return this;
    }

    /// <summary>
    /// 设置过期时间（秒）
    /// </summary>
    public MessageBuilder Expire(int seconds)
    {
        _expireSeconds = seconds;
        return this;
    }

    /// <summary>
    /// 设置事务ID
    /// </summary>
    public MessageBuilder Transaction(string transactionId)
    {
        _transactionId = transactionId;
        return this;
    }

    /// <summary>
    /// 设置优先级（0-9）
    /// </summary>
    public MessageBuilder Priority(int priority)
    {
        _priority = Math.Clamp(priority, 0, 9);
        return this;
    }

    /// <summary>
    /// 构建消息
    /// </summary>
    public IMessage Build()
    {
        if (string.IsNullOrEmpty(_topic))
        {
            throw new InvalidOperationException("Topic is required");
        }

        if (_body is null || _body.Length == 0)
        {
            throw new InvalidOperationException("Body is required");
        }

        return new Message
        {
            MessageId = GenerateMessageId(),
            Topic = _topic,
            Tag = _tag,
            Body = _body,
            Headers = _headers,
            Timestamp = DateTimeOffset.UtcNow,
            PartitionKey = _partitionKey,
            DelaySeconds = _delaySeconds,
            ExpireSeconds = _expireSeconds,
            TransactionId = _transactionId,
            Priority = _priority
        };
    }

    private static string GenerateMessageId()
    {
        // 格式: 时间戳(13位) + 机器标识(4位) + 序列号(8位)
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var machineId = Environment.MachineName.GetHashCode() & 0xFFFF;
        var sequence = Random.Shared.Next(0, 99999999);
        return $"{timestamp:D13}{machineId:X4}{sequence:D8}";
    }
}
