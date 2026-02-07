namespace JZVerse.MicroHuaxia.MessageQueue.Protocol.Tcp;

/// <summary>
/// TCP 帧类型
/// </summary>
public enum TcpFrameType : byte
{
    /// <summary>连接请求</summary>
    Connect = 0x01,
    /// <summary>连接响应</summary>
    ConnectAck = 0x02,
    /// <summary>断开连接</summary>
    Disconnect = 0x03,
    /// <summary>发布消息</summary>
    Publish = 0x10,
    /// <summary>发布响应</summary>
    PublishAck = 0x11,
    /// <summary>批量发布</summary>
    PublishBatch = 0x12,
    /// <summary>订阅</summary>
    Subscribe = 0x20,
    /// <summary>订阅响应</summary>
    SubscribeAck = 0x21,
    /// <summary>取消订阅</summary>
    Unsubscribe = 0x22,
    /// <summary>推送消息</summary>
    Push = 0x30,
    /// <summary>拉取请求</summary>
    Pull = 0x40,
    /// <summary>拉取响应</summary>
    PullResponse = 0x41,
    /// <summary>确认消息</summary>
    Ack = 0x50,
    /// <summary>否定确认</summary>
    Nack = 0x51,
    /// <summary>事务准备</summary>
    TransactionPrepare = 0x60,
    /// <summary>事务准备响应</summary>
    TransactionPrepareAck = 0x61,
    /// <summary>事务提交</summary>
    TransactionCommit = 0x62,
    /// <summary>事务提交响应</summary>
    TransactionCommitAck = 0x63,
    /// <summary>事务回滚</summary>
    TransactionRollback = 0x64,
    /// <summary>事务回滚响应</summary>
    TransactionRollbackAck = 0x65,
    /// <summary>心跳请求</summary>
    Heartbeat = 0xF0,
    /// <summary>心跳响应</summary>
    HeartbeatAck = 0xF1,
    /// <summary>错误响应</summary>
    Error = 0xFF
}

/// <summary>
/// TCP 帧标志
/// </summary>
[Flags]
public enum TcpFrameFlags : ushort
{
    None = 0,
    /// <summary>压缩数据</summary>
    Compressed = 1 << 0,
    /// <summary>需要响应</summary>
    RequiresAck = 1 << 1,
    /// <summary>最后一帧</summary>
    LastFrame = 1 << 2,
    /// <summary>事务消息</summary>
    Transactional = 1 << 3,
    /// <summary>延迟消息</summary>
    Delayed = 1 << 4
}

/// <summary>
/// TCP 帧头 (16 字节)
/// </summary>
/// <remarks>
/// 帧格式:
/// [MagicNumber:4][Version:1][FrameType:1][Flags:2][RequestId:4][BodyLength:4]
/// </remarks>
public readonly struct TcpFrameHeader
{
    /// <summary>魔数 (JZMQ)</summary>
    public const uint MagicNumber = 0x4A5A4D51;

    /// <summary>当前协议版本</summary>
    public const byte CurrentVersion = 1;

    /// <summary>帧头大小</summary>
    public const int HeaderSize = 16;

    /// <summary>协议版本</summary>
    public byte Version { get; init; }

    /// <summary>帧类型</summary>
    public TcpFrameType FrameType { get; init; }

    /// <summary>帧标志</summary>
    public TcpFrameFlags Flags { get; init; }

    /// <summary>请求ID (用于请求-响应匹配)</summary>
    public uint RequestId { get; init; }

    /// <summary>消息体长度</summary>
    public int BodyLength { get; init; }

    public TcpFrameHeader(TcpFrameType frameType, int bodyLength, uint requestId = 0, TcpFrameFlags flags = TcpFrameFlags.None)
    {
        Version = CurrentVersion;
        FrameType = frameType;
        Flags = flags;
        RequestId = requestId;
        BodyLength = bodyLength;
    }
}

/// <summary>
/// TCP 帧
/// </summary>
public sealed class TcpFrame
{
    /// <summary>帧头</summary>
    public TcpFrameHeader Header { get; init; }

    /// <summary>消息体</summary>
    public byte[] Body { get; init; } = [];

    /// <summary>CRC32 校验和</summary>
    public uint Checksum { get; init; }

    public TcpFrame()
    {
    }

    public TcpFrame(TcpFrameType frameType, byte[] body, uint requestId = 0, TcpFrameFlags flags = TcpFrameFlags.None)
    {
        Header = new TcpFrameHeader(frameType, body.Length, requestId, flags);
        Body = body;
        Checksum = CalculateCrc32(body);
    }

    /// <summary>
    /// 计算 CRC32 校验和
    /// </summary>
    public static uint CalculateCrc32(ReadOnlySpan<byte> data)
    {
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
}
