using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;
using JZVerse.MicroHuaxia.Security.Cryptography;

namespace JZVerse.MicroHuaxia.Security;

/// <summary>
/// SM3 国密哈希算法实现
/// </summary>
/// <remarks>
/// 符合 GM/T 0004-2012 标准
/// SM3 是国密密码杂凑算法，输出 256 位哈希值
/// </remarks>
public sealed class Sm3Provider : ISm3Provider
{
    // SM3 初始向量 IV
    private static readonly uint[] IV = new uint[]
    {
        0x7380166F, 0x4914B2B9, 0x172442D7, 0xDA8A0600,
        0xA96F30BC, 0x163138AA, 0xE38DEE4D, 0xB0FB0E4E
    };

    // 常量 T0 和 T1（用于置换函数）
    private const uint T0 = 0x79CC4519;
    private const uint T1 = 0x7A879D8A;

    /// <inheritdoc />
    public byte[] Hash(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Sm3Hash(data);
    }

    /// <inheritdoc />
    public byte[] Hash(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return Hash(Encoding.UTF8.GetBytes(input));
    }

    /// <inheritdoc />
    public string HashToHex(byte[] data)
    {
        var hash = Hash(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <inheritdoc />
    public byte[] Hmac(byte[] data, byte[] key)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(key);

        // HMAC-SM3 实现
        // H(K XOR opad, H(K XOR ipad, message))
        const int blockSize = 64; // SM3 块大小为 512 位 = 64 字节

        // 如果密钥长度大于块大小，先进行哈希
        byte[] normalizedKey = key.Length > blockSize ? Hash(key) : key;

        // 填充密钥到块大小
        byte[] paddedKey = new byte[blockSize];
        Buffer.BlockCopy(normalizedKey, 0, paddedKey, 0, normalizedKey.Length);

        // 计算 ipad 和 opad
        byte[] ipad = new byte[blockSize];
        byte[] opad = new byte[blockSize];
        for (int i = 0; i < blockSize; i++)
        {
            ipad[i] = (byte)(paddedKey[i] ^ 0x36);
            opad[i] = (byte)(paddedKey[i] ^ 0x5C);
        }

        // 计算 H(K XOR ipad, message)
        byte[] innerData = new byte[ipad.Length + data.Length];
        Buffer.BlockCopy(ipad, 0, innerData, 0, ipad.Length);
        Buffer.BlockCopy(data, 0, innerData, ipad.Length, data.Length);
        byte[] innerHash = Hash(innerData);

        // 计算 H(K XOR opad, innerHash)
        byte[] outerData = new byte[opad.Length + innerHash.Length];
        Buffer.BlockCopy(opad, 0, outerData, 0, opad.Length);
        Buffer.BlockCopy(innerHash, 0, outerData, opad.Length, innerHash.Length);
        return Hash(outerData);
    }

    /// <inheritdoc />
    public ISm3HashAlgorithm CreateHashAlgorithm()
    {
        return new Sm3HashAlgorithm();
    }

    /// <summary>
    /// SM3 哈希核心实现
    /// </summary>
    private static byte[] Sm3Hash(byte[] message)
    {
        // 1. 消息填充
        byte[] paddedMessage = PadMessage(message);

        // 2. 消息扩展和压缩
        uint[] v = (uint[])IV.Clone();
        int n = paddedMessage.Length / 64;

        for (int i = 0; i < n; i++)
        {
            byte[] block = new byte[64];
            Buffer.BlockCopy(paddedMessage, i * 64, block, 0, 64);
            Compress(v, block);
        }

        // 3. 输出结果
        byte[] result = new byte[32];
        for (int i = 0; i < 8; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(i * 4), v[i]);
        }

        return result;
    }

    /// <summary>
    /// 消息填充（满足长度 ≡ 448 (mod 512)）
    /// </summary>
    private static byte[] PadMessage(byte[] message)
    {
        ulong bitLength = (ulong)message.Length * 8;
        
        // 计算填充后长度
        int padLength = 64 - (message.Length % 64);
        if (padLength < 9) padLength += 64;

        byte[] padded = new byte[message.Length + padLength];
        Buffer.BlockCopy(message, 0, padded, 0, message.Length);

        // 添加 0x80
        padded[message.Length] = 0x80;

        // 添加原始消息长度（64 位大端序）
        BinaryPrimitives.WriteUInt64BigEndian(
            padded.AsSpan(padded.Length - 8), bitLength);

        return padded;
    }

    /// <summary>
    /// 压缩函数
    /// </summary>
    private static void Compress(uint[] v, byte[] block)
    {
        // 将消息分组转换为 16 个 32 位字
        uint[] w = new uint[68];
        uint[] w1 = new uint[64];

        for (int i = 0; i < 16; i++)
        {
            w[i] = BinaryPrimitives.ReadUInt32BigEndian(block.AsSpan(i * 4));
        }

        // 消息扩展
        for (int j = 16; j < 68; j++)
        {
            w[j] = P1(w[j - 16] ^ w[j - 9] ^ RotateLeft(w[j - 3], 15)) 
                   ^ RotateLeft(w[j - 13], 7) 
                   ^ w[j - 6];
        }

        for (int j = 0; j < 64; j++)
        {
            w1[j] = w[j] ^ w[j + 4];
        }

        // 压缩
        uint a = v[0], b = v[1], c = v[2], d = v[3];
        uint e = v[4], f = v[5], g = v[6], h = v[7];

        for (int j = 0; j < 64; j++)
        {
            uint ss1 = RotateLeft((RotateLeft(a, 12) + e + GetT(j)), 7);
            uint ss2 = ss1 ^ RotateLeft(a, 12);
            uint tt1 = FF(a, b, c, j) + d + ss2 + w1[j];
            uint tt2 = GG(e, f, g, j) + h + ss1 + w[j];
            d = c;
            c = RotateLeft(b, 9);
            b = a;
            a = tt1;
            h = g;
            g = RotateLeft(f, 19);
            f = e;
            e = P0(tt2);
        }

        v[0] ^= a; v[1] ^= b; v[2] ^= c; v[3] ^= d;
        v[4] ^= e; v[5] ^= f; v[6] ^= g; v[7] ^= h;
    }

    /// <summary>
    /// 置换函数 P0
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint P0(uint x)
    {
        return x ^ RotateLeft(x, 9) ^ RotateLeft(x, 17);
    }

    /// <summary>
    /// 置换函数 P1
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint P1(uint x)
    {
        return x ^ RotateLeft(x, 15) ^ RotateLeft(x, 23);
    }

    /// <summary>
    /// 布尔函数 FF
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint FF(uint x, uint y, uint z, int j)
    {
        return j < 16 ? x ^ y ^ z : (x & y) | (x & z) | (y & z);
    }

    /// <summary>
    /// 布尔函数 GG
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GG(uint x, uint y, uint z, int j)
    {
        return j < 16 ? x ^ y ^ z : (x & y) | (~x & z);
    }

    /// <summary>
    /// 获取常量 T
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetT(int j)
    {
        return j < 16 ? T0 : T1;
    }

    /// <summary>
    /// 循环左移
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint RotateLeft(uint value, int bits)
    {
        return (value << bits) | (value >> (32 - bits));
    }

    /// <summary>
    /// SM3 哈希算法实例
    /// </summary>
    private sealed class Sm3HashAlgorithm : ISm3HashAlgorithm
    {
        private readonly List<byte> _buffer = new();
        private readonly uint[] _state;
        private ulong _totalLength;

        public Sm3HashAlgorithm()
        {
            _state = (uint[])IV.Clone();
        }

        public void AppendData(byte[] data)
        {
            _buffer.AddRange(data);
            _totalLength += (ulong)data.Length;
            ProcessBuffer();
        }

        public void AppendData(byte[] data, int offset, int count)
        {
            var segment = new byte[count];
            Buffer.BlockCopy(data, offset, segment, 0, count);
            AppendData(segment);
        }

        public byte[] GetHashAndReset()
        {
            // 填充最终块
            byte[] finalBlock = PadFinalBlock();
            
            // 处理所有完整块
            for (int i = 0; i < finalBlock.Length; i += 64)
            {
                byte[] block = new byte[64];
                Buffer.BlockCopy(finalBlock, i, block, 0, 64);
                Compress(_state, block);
            }

            // 输出结果
            byte[] result = new byte[32];
            for (int i = 0; i < 8; i++)
            {
                BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(i * 4), _state[i]);
            }

            // 重置状态
            Reset();
            return result;
        }

        public void Reset()
        {
            _buffer.Clear();
            Array.Copy(IV, _state, 8);
            _totalLength = 0;
        }

        private void ProcessBuffer()
        {
            while (_buffer.Count >= 64)
            {
                byte[] block = _buffer.Take(64).ToArray();
                Compress(_state, block);
                _buffer.RemoveRange(0, 64);
            }
        }

        private byte[] PadFinalBlock()
        {
            var result = new List<byte>(_buffer);
            ulong bitLength = _totalLength * 8;

            // 添加 0x80
            result.Add(0x80);

            // 计算需要填充的零字节数
            int padLength = 64 - (result.Count % 64);
            if (padLength < 8) padLength += 64;
            padLength -= 8;

            // 填充零字节
            result.AddRange(new byte[padLength]);

            // 添加消息长度
            byte[] lengthBytes = new byte[8];
            BinaryPrimitives.WriteUInt64BigEndian(lengthBytes, bitLength);
            result.AddRange(lengthBytes);

            return result.ToArray();
        }
    }
}
