using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;

namespace JZVerse.MicroHuaxia.MessageQueue.Storage.FileLog;

/// <summary>
/// 消息记录格式（用于持久化）
/// </summary>
/// <remarks>
/// 记录格式:
/// +--------+--------+--------+--------+--------+
/// | Length | CRC32  | Timestamp | MessageData  |
/// | 4B     | 4B     | 8B        | NB           |
/// +--------+--------+--------+--------+--------+
/// </remarks>
public sealed class MessageRecord
{
    /// <summary>
    /// 记录头长度（Length + CRC32 + Timestamp）
    /// </summary>
    public const int HeaderLength = 16;

    /// <summary>
    /// 记录总长度
    /// </summary>
    public int Length { get; init; }

    /// <summary>
    /// CRC32 校验和
    /// </summary>
    public uint Crc32 { get; init; }

    /// <summary>
    /// 时间戳（Unix 毫秒）
    /// </summary>
    public long Timestamp { get; init; }

    /// <summary>
    /// 消息数据
    /// </summary>
    public byte[] Data { get; init; } = [];

    /// <summary>
    /// 从消息创建记录
    /// </summary>
    public static MessageRecord FromMessage(IMessage message)
    {
        var messageData = SerializeMessage(message);
        var timestamp = message.Timestamp.ToUnixTimeMilliseconds();
        var crc = CalculateCrc32(messageData);

        return new MessageRecord
        {
            Length = HeaderLength + messageData.Length,
            Crc32 = crc,
            Timestamp = timestamp,
            Data = messageData
        };
    }

    /// <summary>
    /// 序列化为字节数组
    /// </summary>
    public byte[] ToBytes()
    {
        var buffer = new byte[Length];
        var span = buffer.AsSpan();

        BinaryPrimitives.WriteInt32BigEndian(span[..4], Length);
        BinaryPrimitives.WriteUInt32BigEndian(span[4..8], Crc32);
        BinaryPrimitives.WriteInt64BigEndian(span[8..16], Timestamp);
        Data.CopyTo(span[16..]);

        return buffer;
    }

    /// <summary>
    /// 从字节数组解析
    /// </summary>
    public static MessageRecord FromBytes(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < HeaderLength)
        {
            throw new ArgumentException("Buffer too small for header");
        }

        var length = BinaryPrimitives.ReadInt32BigEndian(buffer[..4]);
        var crc32 = BinaryPrimitives.ReadUInt32BigEndian(buffer[4..8]);
        var timestamp = BinaryPrimitives.ReadInt64BigEndian(buffer[8..16]);
        var data = buffer[16..length].ToArray();

        // 验证 CRC
        var calculatedCrc = CalculateCrc32(data);
        if (calculatedCrc != crc32)
        {
            throw new InvalidDataException($"CRC mismatch: expected {crc32}, got {calculatedCrc}");
        }

        return new MessageRecord
        {
            Length = length,
            Crc32 = crc32,
            Timestamp = timestamp,
            Data = data
        };
    }

    /// <summary>
    /// 解析消息
    /// </summary>
    public IMessage ToMessage()
    {
        return DeserializeMessage(Data);
    }

    private static byte[] SerializeMessage(IMessage message)
    {
        var dto = new MessageDto
        {
            MessageId = message.MessageId,
            Topic = message.Topic,
            Tag = message.Tag,
            Body = Convert.ToBase64String(message.Body),
            Headers = new Dictionary<string, string>(message.Headers),
            Timestamp = message.Timestamp.ToUnixTimeMilliseconds(),
            PartitionKey = message.PartitionKey,
            DelaySeconds = message.DelaySeconds,
            ExpireSeconds = message.ExpireSeconds,
            TransactionId = message.TransactionId,
            Priority = message.Priority
        };

        return JsonSerializer.SerializeToUtf8Bytes(dto);
    }

    private static IMessage DeserializeMessage(byte[] data)
    {
        var dto = JsonSerializer.Deserialize<MessageDto>(data)
            ?? throw new InvalidDataException("Failed to deserialize message");

        return new Message
        {
            MessageId = dto.MessageId,
            Topic = dto.Topic,
            Tag = dto.Tag,
            Body = Convert.FromBase64String(dto.Body),
            Headers = dto.Headers,
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(dto.Timestamp),
            PartitionKey = dto.PartitionKey,
            DelaySeconds = dto.DelaySeconds,
            ExpireSeconds = dto.ExpireSeconds,
            TransactionId = dto.TransactionId,
            Priority = dto.Priority
        };
    }

    private static uint CalculateCrc32(byte[] data)
    {
        // 简单的 CRC32 实现
        uint crc = 0xFFFFFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
            {
                crc = (crc >> 1) ^ (0xEDB88320 & ~((crc & 1) - 1));
            }
        }
        return ~crc;
    }

    /// <summary>
    /// 消息 DTO（用于序列化）
    /// </summary>
    private sealed class MessageDto
    {
        public required string MessageId { get; set; }
        public required string Topic { get; set; }
        public string? Tag { get; set; }
        public required string Body { get; set; }
        public Dictionary<string, string> Headers { get; set; } = new();
        public long Timestamp { get; set; }
        public string? PartitionKey { get; set; }
        public int? DelaySeconds { get; set; }
        public int? ExpireSeconds { get; set; }
        public string? TransactionId { get; set; }
        public int Priority { get; set; }
    }
}
