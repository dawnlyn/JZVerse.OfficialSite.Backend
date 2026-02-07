using System.Buffers;
using System.Buffers.Binary;

namespace JZVerse.MicroHuaxia.MessageQueue.Protocol.Tcp;

/// <summary>
/// TCP 帧编解码器
/// </summary>
public sealed class TcpFrameCodec
{
    /// <summary>最大帧大小 (16MB)</summary>
    public const int MaxFrameSize = 16 * 1024 * 1024;

    /// <summary>
    /// 编码帧到字节数组
    /// </summary>
    public byte[] Encode(TcpFrame frame)
    {
        var totalLength = TcpFrameHeader.HeaderSize + frame.Body.Length + 4; // +4 for CRC32
        var buffer = new byte[totalLength];

        // 写入帧头
        WriteHeader(buffer.AsSpan(), frame.Header);

        // 写入消息体
        if (frame.Body.Length > 0)
        {
            frame.Body.CopyTo(buffer.AsSpan(TcpFrameHeader.HeaderSize));
        }

        // 计算并写入 CRC32
        var checksum = TcpFrame.CalculateCrc32(buffer.AsSpan(0, TcpFrameHeader.HeaderSize + frame.Body.Length));
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(TcpFrameHeader.HeaderSize + frame.Body.Length), checksum);

        return buffer;
    }

    /// <summary>
    /// 从字节序列解码帧
    /// </summary>
    public bool TryDecode(ref ReadOnlySequence<byte> buffer, out TcpFrame? frame)
    {
        frame = null;

        // 检查是否有足够的数据读取帧头
        if (buffer.Length < TcpFrameHeader.HeaderSize)
        {
            return false;
        }

        // 读取帧头
        Span<byte> headerBytes = stackalloc byte[TcpFrameHeader.HeaderSize];
        buffer.Slice(0, TcpFrameHeader.HeaderSize).CopyTo(headerBytes);

        if (!TryReadHeader(headerBytes, out var header))
        {
            throw new InvalidDataException("Invalid frame header");
        }

        // 检查帧大小
        var totalFrameSize = TcpFrameHeader.HeaderSize + header.BodyLength + 4;
        if (header.BodyLength > MaxFrameSize)
        {
            throw new InvalidDataException($"Frame size exceeds maximum: {header.BodyLength}");
        }

        // 检查是否有完整的帧
        if (buffer.Length < totalFrameSize)
        {
            return false;
        }

        // 读取消息体
        var body = new byte[header.BodyLength];
        buffer.Slice(TcpFrameHeader.HeaderSize, header.BodyLength).CopyTo(body);

        // 读取并验证 CRC32
        Span<byte> checksumBytes = stackalloc byte[4];
        buffer.Slice(TcpFrameHeader.HeaderSize + header.BodyLength, 4).CopyTo(checksumBytes);
        var receivedChecksum = BinaryPrimitives.ReadUInt32BigEndian(checksumBytes);

        // 验证校验和
        var expectedChecksum = CalculateFrameChecksum(headerBytes, body);
        if (receivedChecksum != expectedChecksum)
        {
            throw new InvalidDataException("Frame checksum mismatch");
        }

        frame = new TcpFrame
        {
            Header = header,
            Body = body,
            Checksum = receivedChecksum
        };

        // 移动缓冲区位置
        buffer = buffer.Slice(totalFrameSize);
        return true;
    }

    /// <summary>
    /// 从 Span 解码帧 (同步版本)
    /// </summary>
    public TcpFrame Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length < TcpFrameHeader.HeaderSize)
        {
            throw new InvalidDataException("Insufficient data for frame header");
        }

        if (!TryReadHeader(data[..TcpFrameHeader.HeaderSize], out var header))
        {
            throw new InvalidDataException("Invalid frame header");
        }

        var totalFrameSize = TcpFrameHeader.HeaderSize + header.BodyLength + 4;
        if (data.Length < totalFrameSize)
        {
            throw new InvalidDataException("Insufficient data for complete frame");
        }

        var body = data.Slice(TcpFrameHeader.HeaderSize, header.BodyLength).ToArray();
        var receivedChecksum = BinaryPrimitives.ReadUInt32BigEndian(
            data.Slice(TcpFrameHeader.HeaderSize + header.BodyLength, 4));

        return new TcpFrame
        {
            Header = header,
            Body = body,
            Checksum = receivedChecksum
        };
    }

    private static void WriteHeader(Span<byte> buffer, TcpFrameHeader header)
    {
        BinaryPrimitives.WriteUInt32BigEndian(buffer[..4], TcpFrameHeader.MagicNumber);
        buffer[4] = header.Version;
        buffer[5] = (byte)header.FrameType;
        BinaryPrimitives.WriteUInt16BigEndian(buffer[6..8], (ushort)header.Flags);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[8..12], header.RequestId);
        BinaryPrimitives.WriteInt32BigEndian(buffer[12..16], header.BodyLength);
    }

    private static bool TryReadHeader(ReadOnlySpan<byte> data, out TcpFrameHeader header)
    {
        header = default;

        // 验证魔数
        var magic = BinaryPrimitives.ReadUInt32BigEndian(data[..4]);
        if (magic != TcpFrameHeader.MagicNumber)
        {
            return false;
        }

        var version = data[4];
        var frameType = (TcpFrameType)data[5];
        var flags = (TcpFrameFlags)BinaryPrimitives.ReadUInt16BigEndian(data[6..8]);
        var requestId = BinaryPrimitives.ReadUInt32BigEndian(data[8..12]);
        var bodyLength = BinaryPrimitives.ReadInt32BigEndian(data[12..16]);

        if (bodyLength < 0)
        {
            return false;
        }

        header = new TcpFrameHeader
        {
            Version = version,
            FrameType = frameType,
            Flags = flags,
            RequestId = requestId,
            BodyLength = bodyLength
        };

        return true;
    }

    private static uint CalculateFrameChecksum(ReadOnlySpan<byte> header, ReadOnlySpan<byte> body)
    {
        // 计算帧头 + 消息体的 CRC32
        var combined = new byte[header.Length + body.Length];
        header.CopyTo(combined);
        body.CopyTo(combined.AsSpan(header.Length));
        return TcpFrame.CalculateCrc32(combined);
    }
}
