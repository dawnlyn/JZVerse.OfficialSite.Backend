using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using JZVerse.MicroHuaxia.Security.Cryptography;

namespace JZVerse.MicroHuaxia.Security;

/// <summary>
/// SM4 国密对称加密算法实现
/// </summary>
/// <remarks>
/// 符合 GM/T 0002-2012 标准
/// SM4 是国密分组密码算法，分组长度和密钥长度均为 128 位
/// </remarks>
public sealed class Sm4Provider : ISm4Provider
{
    // SM4 S 盒
    private static readonly byte[] Sbox = new byte[]
    {
        0xD6, 0x90, 0xE9, 0xFE, 0xCC, 0xE1, 0x3D, 0xB7, 0x16, 0xB6, 0x14, 0xC2, 0x28, 0xFB, 0x2C, 0x05,
        0x2B, 0x67, 0x9A, 0x76, 0x2A, 0xBE, 0x04, 0xC3, 0xAA, 0x44, 0x13, 0x26, 0x49, 0x86, 0x06, 0x99,
        0x9C, 0x42, 0x50, 0xF4, 0x91, 0xEF, 0x98, 0x7A, 0x33, 0x54, 0x0B, 0x43, 0xED, 0xCF, 0xAC, 0x62,
        0xE4, 0xB3, 0x1C, 0xA9, 0xC9, 0x08, 0xE8, 0x95, 0x80, 0xDF, 0x94, 0xFA, 0x75, 0x8F, 0x3F, 0xA6,
        0x47, 0x07, 0xA7, 0xFC, 0xF3, 0x73, 0x17, 0xBA, 0x83, 0x59, 0x3C, 0x19, 0xE6, 0x85, 0x4F, 0xA8,
        0x68, 0x6B, 0x81, 0xB2, 0x71, 0x64, 0xDA, 0x8B, 0xF8, 0xEB, 0x0F, 0x4B, 0x70, 0x56, 0x9D, 0x35,
        0x1E, 0x24, 0x0E, 0x5E, 0x63, 0x58, 0xD1, 0xA2, 0x25, 0x22, 0x7C, 0x3B, 0x01, 0x21, 0x78, 0x87,
        0xD4, 0x00, 0x46, 0x57, 0x9F, 0xD3, 0x27, 0x52, 0x4C, 0x36, 0x02, 0xE7, 0xA0, 0xC4, 0xC8, 0x9E,
        0xEA, 0xBF, 0x8A, 0xD2, 0x40, 0xC7, 0x38, 0xB5, 0xA3, 0xF7, 0xF2, 0xCE, 0xF9, 0x61, 0x15, 0xA1,
        0xE0, 0xAE, 0x5D, 0xA4, 0x9B, 0x34, 0x1A, 0x55, 0xAD, 0x93, 0x32, 0x30, 0xF5, 0x8C, 0xB1, 0xE3,
        0x1D, 0xF6, 0xE2, 0x2E, 0x82, 0x66, 0xCA, 0x60, 0xC0, 0x29, 0x23, 0xAB, 0x0D, 0x53, 0x4E, 0x6F,
        0xD5, 0xDB, 0x37, 0x45, 0xDE, 0xFD, 0x8E, 0x2F, 0x03, 0xFF, 0x6A, 0x72, 0x6D, 0x6C, 0x5B, 0x51,
        0x8D, 0x1B, 0xAF, 0x92, 0xBB, 0xDD, 0xBC, 0x7F, 0x11, 0xD9, 0x5C, 0x41, 0x1F, 0x10, 0x5A, 0xD8,
        0x0A, 0xC1, 0x31, 0x88, 0xA5, 0xCD, 0x7B, 0xBD, 0x2D, 0x74, 0xD0, 0x12, 0xB8, 0xE5, 0xB4, 0xB0,
        0x89, 0x69, 0x97, 0x4A, 0x0C, 0x96, 0x77, 0x7E, 0x65, 0xB9, 0xF1, 0x09, 0xC5, 0x6E, 0xC6, 0x84,
        0x18, 0xF0, 0x7D, 0xEC, 0x3A, 0xDC, 0x4D, 0x20, 0x79, 0xEE, 0x5F, 0x3E, 0xD7, 0xCB, 0x39, 0x48
    };

    // 轮密钥常量 FK
    private static readonly uint[] FK = new uint[]
    {
        0xA3B1BAC6, 0x56AA3350, 0x677D9197, 0xB27022DC
    };

    // 轮常量 CK
    private static readonly uint[] CK = new uint[]
    {
        0x00070E15, 0x1C232A31, 0x383F464D, 0x545B6269,
        0x70777E85, 0x8C939AA1, 0xA8AFB6BD, 0xC4CBD2D9,
        0xE0E7EEF5, 0xFC030A11, 0x181F262D, 0x343B4249,
        0x50575E65, 0x6C737A81, 0x888F969D, 0xA4ABB2B9,
        0xC0C7CED5, 0xDCE3EAF1, 0xF8FF060D, 0x141B2229,
        0x30373E45, 0x4C535A61, 0x686F767D, 0x848B9299,
        0xA0A7AEB5, 0xBCC3CAD1, 0xD8DFE6ED, 0xF4FB0209,
        0x10171E25, 0x2C333A41, 0x484F565D, 0x646B7279
    };

    /// <inheritdoc />
    public byte[] GenerateKey()
    {
        var key = new byte[16];
        RandomNumberGenerator.Fill(key);
        return key;
    }

    /// <inheritdoc />
    public byte[] GenerateIv()
    {
        var iv = new byte[16];
        RandomNumberGenerator.Fill(iv);
        return iv;
    }

    /// <inheritdoc />
    public byte[] EncryptEcb(byte[] plaintext, byte[] key)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentNullException.ThrowIfNull(key);
        
        if (key.Length != 16)
            throw new ArgumentException("SM4 密钥长度必须为 16 字节", nameof(key));

        byte[] paddedData = Pkcs7Padding(plaintext, 16);
        byte[] ciphertext = new byte[paddedData.Length];
        uint[] roundKeys = GenerateRoundKeys(key);

        for (int i = 0; i < paddedData.Length; i += 16)
        {
            byte[] block = new byte[16];
            Buffer.BlockCopy(paddedData, i, block, 0, 16);
            byte[] encrypted = EncryptBlock(block, roundKeys);
            Buffer.BlockCopy(encrypted, 0, ciphertext, i, 16);
        }

        return ciphertext;
    }

    /// <inheritdoc />
    public byte[] DecryptEcb(byte[] ciphertext, byte[] key)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        ArgumentNullException.ThrowIfNull(key);
        
        if (key.Length != 16)
            throw new ArgumentException("SM4 密钥长度必须为 16 字节", nameof(key));
        
        if (ciphertext.Length % 16 != 0)
            throw new ArgumentException("密文长度必须是 16 的倍数", nameof(ciphertext));

        byte[] plaintext = new byte[ciphertext.Length];
        uint[] roundKeys = GenerateRoundKeys(key);

        for (int i = 0; i < ciphertext.Length; i += 16)
        {
            byte[] block = new byte[16];
            Buffer.BlockCopy(ciphertext, i, block, 0, 16);
            byte[] decrypted = DecryptBlock(block, roundKeys);
            Buffer.BlockCopy(decrypted, 0, plaintext, i, 16);
        }

        return Pkcs7Unpadding(plaintext);
    }

    /// <inheritdoc />
    public byte[] EncryptCbc(byte[] plaintext, byte[] key, byte[] iv)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(iv);
        
        if (key.Length != 16)
            throw new ArgumentException("SM4 密钥长度必须为 16 字节", nameof(key));
        if (iv.Length != 16)
            throw new ArgumentException("SM4 IV 长度必须为 16 字节", nameof(iv));

        byte[] paddedData = Pkcs7Padding(plaintext, 16);
        byte[] ciphertext = new byte[paddedData.Length];
        uint[] roundKeys = GenerateRoundKeys(key);
        byte[] previousBlock = (byte[])iv.Clone();

        for (int i = 0; i < paddedData.Length; i += 16)
        {
            byte[] block = new byte[16];
            Buffer.BlockCopy(paddedData, i, block, 0, 16);
            
            // XOR with previous block
            for (int j = 0; j < 16; j++)
            {
                block[j] ^= previousBlock[j];
            }

            byte[] encrypted = EncryptBlock(block, roundKeys);
            Buffer.BlockCopy(encrypted, 0, ciphertext, i, 16);
            previousBlock = encrypted;
        }

        return ciphertext;
    }

    /// <inheritdoc />
    public byte[] DecryptCbc(byte[] ciphertext, byte[] key, byte[] iv)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(iv);
        
        if (key.Length != 16)
            throw new ArgumentException("SM4 密钥长度必须为 16 字节", nameof(key));
        if (iv.Length != 16)
            throw new ArgumentException("SM4 IV 长度必须为 16 字节", nameof(iv));
        if (ciphertext.Length % 16 != 0)
            throw new ArgumentException("密文长度必须是 16 的倍数", nameof(ciphertext));

        byte[] plaintext = new byte[ciphertext.Length];
        uint[] roundKeys = GenerateRoundKeys(key);
        byte[] previousBlock = (byte[])iv.Clone();

        for (int i = 0; i < ciphertext.Length; i += 16)
        {
            byte[] block = new byte[16];
            Buffer.BlockCopy(ciphertext, i, block, 0, 16);
            byte[] decrypted = DecryptBlock(block, roundKeys);

            // XOR with previous block
            for (int j = 0; j < 16; j++)
            {
                decrypted[j] ^= previousBlock[j];
            }

            Buffer.BlockCopy(decrypted, 0, plaintext, i, 16);
            previousBlock = block;
        }

        return Pkcs7Unpadding(plaintext);
    }

    /// <inheritdoc />
    public byte[] EncryptCtr(byte[] plaintext, byte[] key, byte[] iv)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(iv);
        
        if (key.Length != 16)
            throw new ArgumentException("SM4 密钥长度必须为 16 字节", nameof(key));
        if (iv.Length != 16)
            throw new ArgumentException("SM4 IV 长度必须为 16 字节", nameof(iv));

        byte[] ciphertext = new byte[plaintext.Length];
        uint[] roundKeys = GenerateRoundKeys(key);
        byte[] counter = (byte[])iv.Clone();

        for (int i = 0; i < plaintext.Length; i += 16)
        {
            byte[] keystream = EncryptBlock(counter, roundKeys);
            int blockLength = Math.Min(16, plaintext.Length - i);

            for (int j = 0; j < blockLength; j++)
            {
                ciphertext[i + j] = (byte)(plaintext[i + j] ^ keystream[j]);
            }

            // Increment counter
            IncrementCounter(counter);
        }

        return ciphertext;
    }

    /// <inheritdoc />
    public byte[] DecryptCtr(byte[] ciphertext, byte[] key, byte[] iv)
    {
        // CTR 模式加密和解密相同
        return EncryptCtr(ciphertext, key, iv);
    }

    /// <inheritdoc />
    public (byte[] ciphertext, byte[] tag) EncryptGcm(
        byte[] plaintext, 
        byte[] key, 
        byte[] nonce, 
        byte[]? associatedData = null)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(nonce);
        
        if (key.Length != 16)
            throw new ArgumentException("SM4 密钥长度必须为 16 字节", nameof(key));
        if (nonce.Length != 12)
            throw new ArgumentException("SM4 GCM nonce 长度必须为 12 字节", nameof(nonce));

        // GCM 模式实现较为复杂，这里使用简化版本
        // 实际生产环境需要完整实现 GHASH 函数
        uint[] roundKeys = GenerateRoundKeys(key);
        
        // 生成 IV
        byte[] iv = new byte[16];
        Buffer.BlockCopy(nonce, 0, iv, 0, 12);
        iv[15] = 1;

        // 加密明文
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] counter = (byte[])iv.Clone();
        
        for (int i = 0; i < plaintext.Length; i += 16)
        {
            byte[] keystream = EncryptBlock(counter, roundKeys);
            int blockLength = Math.Min(16, plaintext.Length - i);

            for (int j = 0; j < blockLength; j++)
            {
                ciphertext[i + j] = (byte)(plaintext[i + j] ^ keystream[j]);
            }

            IncrementCounter(counter);
        }

        // 生成认证标签（简化实现，实际应使用 GHASH）
        byte[] tag = GenerateTag(plaintext, ciphertext, associatedData, key, iv);

        return (ciphertext, tag);
    }

    /// <inheritdoc />
    public byte[] DecryptGcm(
        byte[] ciphertext, 
        byte[] key, 
        byte[] nonce, 
        byte[] tag, 
        byte[]? associatedData = null)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(nonce);
        ArgumentNullException.ThrowIfNull(tag);
        
        if (key.Length != 16)
            throw new ArgumentException("SM4 密钥长度必须为 16 字节", nameof(key));
        if (nonce.Length != 12)
            throw new ArgumentException("SM4 GCM nonce 长度必须为 12 字节", nameof(nonce));
        if (tag.Length != 16)
            throw new ArgumentException("SM4 GCM 认证标签长度必须为 16 字节", nameof(tag));

        uint[] roundKeys = GenerateRoundKeys(key);
        
        // 生成 IV
        byte[] iv = new byte[16];
        Buffer.BlockCopy(nonce, 0, iv, 0, 12);
        iv[15] = 1;

        // 解密密文
        byte[] plaintext = new byte[ciphertext.Length];
        byte[] counter = (byte[])iv.Clone();
        
        for (int i = 0; i < ciphertext.Length; i += 16)
        {
            byte[] keystream = EncryptBlock(counter, roundKeys);
            int blockLength = Math.Min(16, ciphertext.Length - i);

            for (int j = 0; j < blockLength; j++)
            {
                plaintext[i + j] = (byte)(ciphertext[i + j] ^ keystream[j]);
            }

            IncrementCounter(counter);
        }

        // 验证认证标签（简化实现）
        byte[] computedTag = GenerateTag(plaintext, ciphertext, associatedData, key, iv);
        if (!computedTag.SequenceEqual(tag))
        {
            throw new CryptographicException("GCM 认证失败：标签不匹配");
        }

        return plaintext;
    }

    #region 核心加密函数

    /// <summary>
    /// 生成轮密钥
    /// </summary>
    private static uint[] GenerateRoundKeys(byte[] key)
    {
        uint[] K = new uint[36];
        uint[] rk = new uint[32];

        // 初始化
        for (int i = 0; i < 4; i++)
        {
            K[i] = BinaryPrimitives.ReadUInt32BigEndian(key.AsSpan(i * 4)) ^ FK[i];
        }

        // 轮密钥扩展
        for (int i = 0; i < 32; i++)
        {
            K[i + 4] = K[i] ^ TPrime(K[i + 1] ^ K[i + 2] ^ K[i + 3] ^ CK[i]);
            rk[i] = K[i + 4];
        }

        return rk;
    }

    /// <summary>
    /// 加密单块数据
    /// </summary>
    private static byte[] EncryptBlock(byte[] input, uint[] roundKeys)
    {
        uint[] X = new uint[36];

        // 加载输入
        for (int i = 0; i < 4; i++)
        {
            X[i] = BinaryPrimitives.ReadUInt32BigEndian(input.AsSpan(i * 4));
        }

        // 32 轮加密
        for (int i = 0; i < 32; i++)
        {
            X[i + 4] = F(X[i], X[i + 1], X[i + 2], X[i + 3], roundKeys[i]);
        }

        // 反序变换
        byte[] output = new byte[16];
        for (int i = 0; i < 4; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(i * 4), X[35 - i]);
        }

        return output;
    }

    /// <summary>
    /// 解密单块数据
    /// </summary>
    private static byte[] DecryptBlock(byte[] input, uint[] roundKeys)
    {
        uint[] X = new uint[36];

        // 加载输入
        for (int i = 0; i < 4; i++)
        {
            X[i] = BinaryPrimitives.ReadUInt32BigEndian(input.AsSpan(i * 4));
        }

        // 32 轮解密（轮密钥逆序使用）
        for (int i = 0; i < 32; i++)
        {
            X[i + 4] = F(X[i], X[i + 1], X[i + 2], X[i + 3], roundKeys[31 - i]);
        }

        // 反序变换
        byte[] output = new byte[16];
        for (int i = 0; i < 4; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(i * 4), X[35 - i]);
        }

        return output;
    }

    /// <summary>
    /// 轮函数 F
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint F(uint x0, uint x1, uint x2, uint x3, uint rk)
    {
        return x0 ^ T(x1 ^ x2 ^ x3 ^ rk);
    }

    /// <summary>
    /// 合成置换 T
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint T(uint value)
    {
        return L(Tau(value));
    }

    /// <summary>
    /// 合成置换 T'（用于密钥扩展）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint TPrime(uint value)
    {
        return LPrime(Tau(value));
    }

    /// <summary>
    /// 非线性变换 tau
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Tau(uint value)
    {
        return (uint)(Sbox[(value >> 24) & 0xFF] << 24 |
                     Sbox[(value >> 16) & 0xFF] << 16 |
                     Sbox[(value >> 8) & 0xFF] << 8 |
                     Sbox[value & 0xFF]);
    }

    /// <summary>
    /// 线性变换 L
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint L(uint value)
    {
        return value ^ RotateLeft(value, 2) ^ RotateLeft(value, 10) 
               ^ RotateLeft(value, 18) ^ RotateLeft(value, 24);
    }

    /// <summary>
    /// 线性变换 L'（用于密钥扩展）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint LPrime(uint value)
    {
        return value ^ RotateLeft(value, 13) ^ RotateLeft(value, 23);
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
    /// 增加计数器
    /// </summary>
    private static void IncrementCounter(byte[] counter)
    {
        for (int i = counter.Length - 1; i >= 0; i--)
        {
            if (++counter[i] != 0)
                break;
        }
    }

    /// <summary>
    /// PKCS7 填充
    /// </summary>
    private static byte[] Pkcs7Padding(byte[] data, int blockSize)
    {
        int paddingLength = blockSize - (data.Length % blockSize);
        byte[] padded = new byte[data.Length + paddingLength];
        Buffer.BlockCopy(data, 0, padded, 0, data.Length);
        for (int i = data.Length; i < padded.Length; i++)
        {
            padded[i] = (byte)paddingLength;
        }
        return padded;
    }

    /// <summary>
    /// PKCS7 去填充
    /// </summary>
    private static byte[] Pkcs7Unpadding(byte[] data)
    {
        int paddingLength = data[data.Length - 1];
        byte[] unpadded = new byte[data.Length - paddingLength];
        Buffer.BlockCopy(data, 0, unpadded, 0, unpadded.Length);
        return unpadded;
    }

    /// <summary>
    /// 生成认证标签（简化实现）
    /// </summary>
    private static byte[] GenerateTag(
        byte[] plaintext, 
        byte[] ciphertext, 
        byte[]? associatedData, 
        byte[] key, 
        byte[] iv)
    {
        // 注意：这是简化实现
        // 实际 GCM 需要使用 GHASH 函数
        using var sm3 = System.Security.Cryptography.SHA256.Create();
        sm3.TransformBlock(iv, 0, iv.Length, null, 0);
        sm3.TransformBlock(key, 0, key.Length, null, 0);
        if (associatedData != null)
        {
            sm3.TransformBlock(associatedData, 0, associatedData.Length, null, 0);
        }
        sm3.TransformBlock(ciphertext, 0, ciphertext.Length, null, 0);
        sm3.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        
        byte[] hash = sm3.Hash!;
        byte[] tag = new byte[16];
        Buffer.BlockCopy(hash, 0, tag, 0, 16);
        return tag;
    }

    #endregion
}
