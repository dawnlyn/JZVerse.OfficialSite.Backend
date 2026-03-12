using System.Buffers.Binary;
using System.Security.Cryptography;

namespace JZVerse.DataAccess.Abstractions.Models;

/// <summary>
/// 128 位雪花 ID 值对象，兼容 UUID/GUID 格式
/// </summary>
/// <remarks>
/// 位结构：
/// 高 64 位: [符号位1][时间戳42位][数据中心ID5位][机器ID5位][序列号11位]
/// 低 64 位: [随机填充64位]
/// </remarks>
public readonly struct SnowflakeId : IEquatable<SnowflakeId>, IComparable<SnowflakeId>
{
    /// <summary>
    /// 起始时间戳 (2024-01-01 00:00:00 UTC)
    /// </summary>
    public static readonly DateTimeOffset Epoch = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly byte[] _value;

    /// <summary>
    /// 空 ID
    /// </summary>
    public static readonly SnowflakeId Empty = new(new byte[16]);

    /// <summary>
    /// 从字节数组创建雪花 ID
    /// </summary>
    public SnowflakeId(byte[] value)
    {
        if (value.Length != 16)
            throw new ArgumentException("SnowflakeId must be 16 bytes", nameof(value));
        _value = value;
    }

    /// <summary>
    /// 从各个组成部分创建雪花 ID
    /// </summary>
    /// <param name="timestamp">时间戳（毫秒，相对于 Epoch）</param>
    /// <param name="datacenterId">数据中心 ID (0-31)</param>
    /// <param name="workerId">机器 ID (0-31)</param>
    /// <param name="sequence">序列号 (0-2047)</param>
    public SnowflakeId(long timestamp, int datacenterId, int workerId, int sequence)
    {
        if (timestamp < 0)
            throw new ArgumentOutOfRangeException(nameof(timestamp), "Timestamp must be non-negative");
        if (datacenterId is < 0 or > 31)
            throw new ArgumentOutOfRangeException(nameof(datacenterId), "DatacenterId must be between 0 and 31");
        if (workerId is < 0 or > 31)
            throw new ArgumentOutOfRangeException(nameof(workerId), "WorkerId must be between 0 and 31");
        if (sequence is < 0 or > 2047)
            throw new ArgumentOutOfRangeException(nameof(sequence), "Sequence must be between 0 and 2047");

        _value = new byte[16];

        // 构建高 64 位
        // [0][timestamp:42bits][datacenterId:5bits][workerId:5bits][sequence:11bits]
        long high = (timestamp & 0x3FFFFFFFFFFL) << 21;  // 42 bits timestamp
        high |= (long)(datacenterId & 0x1F) << 16;       // 5 bits datacenter
        high |= (long)(workerId & 0x1F) << 11;           // 5 bits worker
        high |= (long)(sequence & 0x7FF);                // 11 bits sequence

        // 构建低 64 位 (随机填充)
        Span<byte> randomBytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(randomBytes);
        long low = BinaryPrimitives.ReadInt64BigEndian(randomBytes);

        // 写入字节数组 (大端序)
        BinaryPrimitives.WriteInt64BigEndian(_value.AsSpan(0, 8), high);
        BinaryPrimitives.WriteInt64BigEndian(_value.AsSpan(8, 8), low);
    }

    /// <summary>
    /// 获取原始字节值
    /// </summary>
    public ReadOnlySpan<byte> Value => _value ?? [];

    /// <summary>
    /// 转换为 GUID
    /// </summary>
    public Guid ToGuid()
    {
        if (_value is null || _value.Length != 16)
            return Guid.Empty;
        return new Guid(_value);
    }

    /// <summary>
    /// 从 GUID 创建雪花 ID
    /// </summary>
    public static SnowflakeId FromGuid(Guid guid)
    {
        return new SnowflakeId(guid.ToByteArray());
    }

    /// <summary>
    /// 提取时间戳
    /// </summary>
    public DateTimeOffset GetTimestamp()
    {
        if (_value is null || _value.Length != 16)
            return Epoch;

        long high = BinaryPrimitives.ReadInt64BigEndian(_value.AsSpan(0, 8));
        long timestamp = (high >> 21) & 0x3FFFFFFFFFFL;
        return Epoch.AddMilliseconds(timestamp);
    }

    /// <summary>
    /// 提取数据中心 ID
    /// </summary>
    public int GetDatacenterId()
    {
        if (_value is null || _value.Length != 16)
            return 0;

        long high = BinaryPrimitives.ReadInt64BigEndian(_value.AsSpan(0, 8));
        return (int)((high >> 16) & 0x1F);
    }

    /// <summary>
    /// 提取机器 ID
    /// </summary>
    public int GetWorkerId()
    {
        if (_value is null || _value.Length != 16)
            return 0;

        long high = BinaryPrimitives.ReadInt64BigEndian(_value.AsSpan(0, 8));
        return (int)((high >> 11) & 0x1F);
    }

    /// <summary>
    /// 提取序列号
    /// </summary>
    public int GetSequence()
    {
        if (_value is null || _value.Length != 16)
            return 0;

        long high = BinaryPrimitives.ReadInt64BigEndian(_value.AsSpan(0, 8));
        return (int)(high & 0x7FF);
    }

    /// <summary>
    /// 转换为字符串 (32位十六进制)
    /// </summary>
    public override string ToString()
    {
        if (_value is null || _value.Length != 16)
            return new string('0', 32);
        return Convert.ToHexString(_value).ToLowerInvariant();
    }

    /// <summary>
    /// 从字符串解析
    /// </summary>
    public static SnowflakeId Parse(string value)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentNullException(nameof(value));

        // 支持 GUID 格式 (带连字符)
        if (value.Contains('-'))
        {
            return FromGuid(Guid.Parse(value));
        }

        // 32位十六进制格式
        if (value.Length != 32)
            throw new ArgumentException("SnowflakeId string must be 32 hex characters", nameof(value));

        return new SnowflakeId(Convert.FromHexString(value));
    }

    /// <summary>
    /// 尝试从字符串解析
    /// </summary>
    public static bool TryParse(string? value, out SnowflakeId result)
    {
        result = Empty;

        if (string.IsNullOrEmpty(value))
            return false;

        try
        {
            result = Parse(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool Equals(SnowflakeId other)
    {
        if (_value is null && other._value is null)
            return true;
        if (_value is null || other._value is null)
            return false;
        return _value.AsSpan().SequenceEqual(other._value);
    }

    public override bool Equals(object? obj) => obj is SnowflakeId other && Equals(other);

    public override int GetHashCode()
    {
        if (_value is null || _value.Length != 16)
            return 0;

        var hash = new HashCode();
        hash.AddBytes(_value);
        return hash.ToHashCode();
    }

    public int CompareTo(SnowflakeId other)
    {
        if (_value is null && other._value is null)
            return 0;
        if (_value is null)
            return -1;
        if (other._value is null)
            return 1;

        return _value.AsSpan().SequenceCompareTo(other._value);
    }

    public static bool operator ==(SnowflakeId left, SnowflakeId right) => left.Equals(right);
    public static bool operator !=(SnowflakeId left, SnowflakeId right) => !left.Equals(right);
    public static bool operator <(SnowflakeId left, SnowflakeId right) => left.CompareTo(right) < 0;
    public static bool operator >(SnowflakeId left, SnowflakeId right) => left.CompareTo(right) > 0;
    public static bool operator <=(SnowflakeId left, SnowflakeId right) => left.CompareTo(right) <= 0;
    public static bool operator >=(SnowflakeId left, SnowflakeId right) => left.CompareTo(right) >= 0;

    public static implicit operator Guid(SnowflakeId id) => id.ToGuid();
    public static explicit operator SnowflakeId(Guid guid) => FromGuid(guid);
}
