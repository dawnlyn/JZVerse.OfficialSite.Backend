using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using JZVerse.MicroHuaxia.Security.Cryptography;

namespace JZVerse.MicroHuaxia.Security;

/// <summary>
/// SM2 国密椭圆曲线算法实现
/// </summary>
/// <remarks>
/// 符合 GM/T 0003-2012 标准
/// SM2 使用 256 位椭圆曲线，曲线参数定义在国密标准中
/// </remarks>
public sealed class Sm2Provider : ISm2Provider
{
    // SM2 椭圆曲线参数（国密标准 GM/T 0003.5-2012）
    private static readonly BigInteger P = ParseHex("FFFFFFFEFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF00000000FFFFFFFFFFFFFFFF");
    private static readonly BigInteger A = ParseHex("FFFFFFFEFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF00000000FFFFFFFFFFFFFFFC");
    private static readonly BigInteger B = ParseHex("28E9FA9E9D9F5E344D5A9E4BCF6509A7F39789F515AB8F92DDBCBD414D940E93");
    private static readonly BigInteger N = ParseHex("FFFFFFFEFFFFFFFFFFFFFFFFFFFFFFFF7203DF6B21C6052B53BBF40939D54123");
    private static readonly BigInteger Gx = ParseHex("32C4AE2C1F1981195F9904466A39C9948FE30BBFF2660BE1715A4589334C74C7");
    private static readonly BigInteger Gy = ParseHex("BC3736A2F4F6779C59BDCEE36B692153D0A9877CC62A474002DF32E52139F0A0");

    // 基点 G
    private static readonly EcPoint G = new(Gx, Gy);

    /// <inheritdoc />
    public (byte[] publicKey, byte[] privateKey) GenerateKeyPair()
    {
        // 生成随机私钥 d (1 <= d < n-1)
        byte[] dBytes = new byte[32];
        BigInteger d;
        
        do
        {
            RandomNumberGenerator.Fill(dBytes);
            d = new BigInteger(dBytes, true, true);
        } while (d <= 0 || d >= N);

        // 计算公钥 P = d * G
        EcPoint publicKeyPoint = ScalarMultiply(G, d);

        // 序列化密钥
        byte[] privateKey = BigIntegerToBytes(d, 32);
        byte[] publicKey = PointToBytes(publicKeyPoint, false);

        return (publicKey, privateKey);
    }

    /// <inheritdoc />
    public byte[] Encrypt(byte[] plaintext, byte[] publicKey)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentNullException.ThrowIfNull(publicKey);

        // 解析公钥
        EcPoint publicKeyPoint = BytesToPoint(publicKey);

        // 生成随机数 k
        byte[] kBytes = new byte[32];
        BigInteger k;
        do
        {
            RandomNumberGenerator.Fill(kBytes);
            k = new BigInteger(kBytes, true, true);
        } while (k <= 0 || k >= N);

        // 计算 C1 = k * G
        EcPoint c1Point = ScalarMultiply(G, k);
        byte[] c1 = PointToBytes(c1Point, false);

        // 计算 S = k * P (公钥点)
        EcPoint sPoint = ScalarMultiply(publicKeyPoint, k);
        byte[] sBytes = PointToBytes(sPoint, false);

        // 使用 SM3 派生密钥
        byte[] kdfResult = Kdf(sBytes, plaintext.Length);

        // 计算 C2 = M XOR t
        byte[] c2 = new byte[plaintext.Length];
        for (int i = 0; i < plaintext.Length; i++)
        {
            c2[i] = (byte)(plaintext[i] ^ kdfResult[i]);
        }

        // 计算 C3 = SM3(x2 || M || y2)
        byte[] x2Bytes = BigIntegerToBytes(sPoint.X, 32);
        byte[] y2Bytes = BigIntegerToBytes(sPoint.Y, 32);
        byte[] c3 = Sm3Hash(x2Bytes, plaintext, y2Bytes);

        // 组装密文: C1 || C3 || C2
        byte[] ciphertext = new byte[c1.Length + c3.Length + c2.Length];
        Buffer.BlockCopy(c1, 0, ciphertext, 0, c1.Length);
        Buffer.BlockCopy(c3, 0, ciphertext, c1.Length, c3.Length);
        Buffer.BlockCopy(c2, 0, ciphertext, c1.Length + c3.Length, c2.Length);

        return ciphertext;
    }

    /// <inheritdoc />
    public byte[] Decrypt(byte[] ciphertext, byte[] privateKey)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        ArgumentNullException.ThrowIfNull(privateKey);

        if (privateKey.Length != 32)
            throw new ArgumentException("私钥长度必须为 32 字节", nameof(privateKey));

        // 解析密文
        // C1: 65 字节（未压缩格式: 0x04 || x || y）
        // C3: 32 字节（SM3 哈希）
        // C2: 剩余字节
        if (ciphertext.Length < 65 + 32 + 1)
            throw new ArgumentException("密文长度无效", nameof(ciphertext));

        byte[] c1 = ciphertext[..65];
        byte[] c3 = ciphertext[65..97];
        byte[] c2 = ciphertext[97..];

        // 解析 C1 点
        EcPoint c1Point = BytesToPoint(c1);

        // 解析私钥
        BigInteger d = new(privateKey, true, true);

        // 验证 C1 在曲线上
        if (!IsPointOnCurve(c1Point))
            throw new CryptographicException("解密失败：C1 不在椭圆曲线上");

        // 计算 S = d * C1
        EcPoint sPoint = ScalarMultiply(c1Point, d);
        byte[] sBytes = PointToBytes(sPoint, false);

        // 使用 SM3 派生密钥
        byte[] kdfResult = Kdf(sBytes, c2.Length);

        // 计算明文 M = C2 XOR t
        byte[] plaintext = new byte[c2.Length];
        for (int i = 0; i < c2.Length; i++)
        {
            plaintext[i] = (byte)(c2[i] ^ kdfResult[i]);
        }

        // 验证 C3
        byte[] x2Bytes = BigIntegerToBytes(sPoint.X, 32);
        byte[] y2Bytes = BigIntegerToBytes(sPoint.Y, 32);
        byte[] computedC3 = Sm3Hash(x2Bytes, plaintext, y2Bytes);

        if (!computedC3.SequenceEqual(c3))
            throw new CryptographicException("解密失败：哈希验证不匹配");

        return plaintext;
    }

    /// <inheritdoc />
    public byte[] Sign(byte[] data, byte[] privateKey)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(privateKey);

        if (privateKey.Length != 32)
            throw new ArgumentException("私钥长度必须为 32 字节", nameof(privateKey));

        // 计算 e = SM3(Z || M)
        byte[] z = GetZ(null, BytesToPoint(PointToBytes(ScalarMultiply(G, new BigInteger(privateKey, true, true)), false)));
        byte[] e = Sm3Hash(z, data);
        BigInteger eInt = new(e, true, true);

        // 解析私钥
        BigInteger d = new(privateKey, true, true);

        // 生成签名
        byte[] kBytes = new byte[32];
        BigInteger k, r, s;

        do
        {
            do
            {
                // 生成随机数 k
                do
                {
                    RandomNumberGenerator.Fill(kBytes);
                    k = new BigInteger(kBytes, true, true);
                } while (k <= 0 || k >= N);

                // 计算 (x1, y1) = k * G
                EcPoint kG = ScalarMultiply(G, k);
                r = Mod(kG.X + eInt, N);
            } while (r == 0 || r + k == N);

            // 计算 s = (1 + d)^-1 * (k - r * d) mod n
            BigInteger dPlus1Inv = ModInverse(1 + d, N);
            s = Mod(dPlus1Inv * (k - r * d), N);
        } while (s == 0);

        // 序列化签名: r || s
        byte[] signature = new byte[64];
        Buffer.BlockCopy(BigIntegerToBytes(r, 32), 0, signature, 0, 32);
        Buffer.BlockCopy(BigIntegerToBytes(s, 32), 0, signature, 32, 32);

        return signature;
    }

    /// <inheritdoc />
    public bool Verify(byte[] data, byte[] signature, byte[] publicKey)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(publicKey);

        if (signature.Length != 64)
            throw new ArgumentException("签名长度必须为 64 字节", nameof(signature));

        // 解析签名
        BigInteger r = new(signature[..32], true, true);
        BigInteger s = new(signature[32..], true, true);

        if (r <= 0 || r >= N || s <= 0 || s >= N)
            return false;

        // 解析公钥
        EcPoint publicKeyPoint = BytesToPoint(publicKey);

        // 验证公钥在曲线上
        if (!IsPointOnCurve(publicKeyPoint))
            return false;

        // 计算 e = SM3(Z || M)
        byte[] z = GetZ(null, publicKeyPoint);
        byte[] e = Sm3Hash(z, data);
        BigInteger eInt = new(e, true, true);

        // 验证签名
        BigInteger t = Mod(r + s, N);
        if (t == 0)
            return false;

        // 计算 (x1, y1) = s * G + t * P
        EcPoint sG = ScalarMultiply(G, s);
        EcPoint tP = ScalarMultiply(publicKeyPoint, t);
        EcPoint point = PointAdd(sG, tP);

        // 验证 r == (e + x1) mod n
        BigInteger computedR = Mod(eInt + point.X, N);
        return computedR == r;
    }

    /// <inheritdoc />
    public string ExportPublicKey(byte[] publicKey)
    {
        return Convert.ToHexString(publicKey).ToLowerInvariant();
    }

    /// <inheritdoc />
    public byte[] ImportPublicKey(string publicKeyBase64)
    {
        // 尝试 Hex 解码
        if (publicKeyBase64.Length == 130 || publicKeyBase64.Length == 128)
        {
            if (publicKeyBase64.Length == 130 && publicKeyBase64.StartsWith("04"))
            {
                return Convert.FromHexString(publicKeyBase64);
            }
            if (publicKeyBase64.Length == 128)
            {
                return Convert.FromHexString("04" + publicKeyBase64);
            }
        }

        // 尝试 Base64 解码
        try
        {
            byte[] key = Convert.FromBase64String(publicKeyBase64);
            if (key.Length == 65 || key.Length == 64)
            {
                if (key.Length == 65 && key[0] == 0x04)
                    return key;
                if (key.Length == 64)
                {
                    byte[] fullKey = new byte[65];
                    fullKey[0] = 0x04;
                    Buffer.BlockCopy(key, 0, fullKey, 1, 64);
                    return fullKey;
                }
            }
        }
        catch { }

        throw new ArgumentException("无效的公钥格式", nameof(publicKeyBase64));
    }

    /// <inheritdoc />
    public string ExportPrivateKey(byte[] privateKey, string? password = null)
    {
        if (string.IsNullOrEmpty(password))
        {
            return Convert.ToHexString(privateKey).ToLowerInvariant();
        }

        // 使用密码加密私钥
        var sm4 = new Sm4Provider();
        byte[] key = DeriveKeyFromPassword(password);
        byte[] iv = sm4.GenerateIv();
        byte[] encrypted = sm4.EncryptCbc(privateKey, key, iv);
        
        byte[] result = new byte[iv.Length + encrypted.Length];
        Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
        Buffer.BlockCopy(encrypted, 0, result, iv.Length, encrypted.Length);
        
        return Convert.ToHexString(result).ToLowerInvariant();
    }

    /// <inheritdoc />
    public byte[] ImportPrivateKey(string privateKeyEncrypted, string? password = null)
    {
        byte[] data = Convert.FromHexString(privateKeyEncrypted);

        if (string.IsNullOrEmpty(password))
        {
            if (data.Length != 32)
                throw new ArgumentException("私钥长度无效", nameof(privateKeyEncrypted));
            return data;
        }

        // 使用密码解密私钥
        var sm4 = new Sm4Provider();
        byte[] key = DeriveKeyFromPassword(password);
        byte[] iv = data[..16];
        byte[] encrypted = data[16..];
        
        return sm4.DecryptCbc(encrypted, key, iv);
    }

    #region 椭圆曲线运算

    /// <summary>
    /// 椭圆曲线点
    /// </summary>
    private readonly record struct EcPoint(BigInteger X, BigInteger Y);

    /// <summary>
    /// 标量乘法: k * P
    /// </summary>
    private static EcPoint ScalarMultiply(EcPoint point, BigInteger k)
    {
        if (k == 0)
            throw new ArgumentException("标量不能为零");

        EcPoint result = new(BigInteger.Zero, BigInteger.Zero);
        EcPoint addend = point;
        bool isFirst = true;

        while (k > 0)
        {
            if ((k & 1) == 1)
            {
                if (isFirst)
                {
                    result = addend;
                    isFirst = false;
                }
                else
                {
                    result = PointAdd(result, addend);
                }
            }

            addend = PointDouble(addend);
            k >>= 1;
        }

        return result;
    }

    /// <summary>
    /// 点加法
    /// </summary>
    private static EcPoint PointAdd(EcPoint p1, EcPoint p2)
    {
        if (p1.X == 0 && p1.Y == 0) return p2;
        if (p2.X == 0 && p2.Y == 0) return p1;

        if (p1.X == p2.X)
        {
            if (p1.Y == p2.Y)
                return PointDouble(p1);
            return new EcPoint(BigInteger.Zero, BigInteger.Zero); // 无穷远点
        }

        BigInteger lambda = Mod((p2.Y - p1.Y) * ModInverse(p2.X - p1.X, P), P);
        BigInteger x3 = Mod(lambda * lambda - p1.X - p2.X, P);
        BigInteger y3 = Mod(lambda * (p1.X - x3) - p1.Y, P);

        return new EcPoint(x3, y3);
    }

    /// <summary>
    /// 点倍乘
    /// </summary>
    private static EcPoint PointDouble(EcPoint p)
    {
        if (p.Y == 0)
            return new EcPoint(BigInteger.Zero, BigInteger.Zero);

        BigInteger lambda = Mod((3 * p.X * p.X + A) * ModInverse(2 * p.Y, P), P);
        BigInteger x3 = Mod(lambda * lambda - 2 * p.X, P);
        BigInteger y3 = Mod(lambda * (p.X - x3) - p.Y, P);

        return new EcPoint(x3, y3);
    }

    /// <summary>
    /// 检查点是否在曲线上
    /// </summary>
    private static bool IsPointOnCurve(EcPoint point)
    {
        BigInteger lhs = Mod(point.Y * point.Y, P);
        BigInteger rhs = Mod(point.X * point.X * point.X + A * point.X + B, P);
        return lhs == rhs;
    }

    #endregion

    #region 辅助函数

    /// <summary>
    /// 密钥派生函数 KDF
    /// </summary>
    private static byte[] Kdf(byte[] z, int keyLen)
    {
        byte[] result = new byte[keyLen];
        int ct = 1;
        int offset = 0;

        while (offset < keyLen)
        {
            byte[] counterBytes = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(counterBytes, ct);
            
            byte[] hashInput = new byte[z.Length + 4];
            Buffer.BlockCopy(z, 0, hashInput, 0, z.Length);
            Buffer.BlockCopy(counterBytes, 0, hashInput, z.Length, 4);
            
            byte[] hash = Sm3Hash(hashInput);
            int copyLen = Math.Min(32, keyLen - offset);
            Buffer.BlockCopy(hash, 0, result, offset, copyLen);
            
            offset += copyLen;
            ct++;
        }

        return result;
    }

    /// <summary>
    /// 计算 Z 值
    /// </summary>
    private static byte[] GetZ(byte[]? id, EcPoint publicKey)
    {
        // 默认 ID: 1234567812345678
        byte[] idBytes = id ?? Encoding.ASCII.GetBytes("1234567812345678");
        ushort idLen = (ushort)(idBytes.Length * 8); // ID 长度（比特）
        
        // Z = SM3(ENTL || ID || a || b || xG || yG || xA || yA)
        using var ms = new MemoryStream();
        ms.Write(BitConverter.GetBytes(idLen).Reverse().ToArray()); // 2 bytes
        ms.Write(idBytes);
        ms.Write(BigIntegerToBytes(A, 32));
        ms.Write(BigIntegerToBytes(B, 32));
        ms.Write(BigIntegerToBytes(Gx, 32));
        ms.Write(BigIntegerToBytes(Gy, 32));
        ms.Write(BigIntegerToBytes(publicKey.X, 32));
        ms.Write(BigIntegerToBytes(publicKey.Y, 32));

        return Sm3Hash(ms.ToArray());
    }

    /// <summary>
    /// SM3 哈希计算
    /// </summary>
    private static byte[] Sm3Hash(params byte[][] data)
    {
        using var ms = new MemoryStream();
        foreach (var chunk in data)
        {
            ms.Write(chunk);
        }
        
        var sm3 = new Sm3Provider();
        return sm3.Hash(ms.ToArray());
    }

    /// <summary>
    /// 将点转换为字节数组
    /// </summary>
    private static byte[] PointToBytes(EcPoint point, bool compressed)
    {
        if (compressed)
        {
            byte[] result = new byte[33];
            result[0] = (byte)(point.Y.IsEven ? 0x02 : 0x03);
            Buffer.BlockCopy(BigIntegerToBytes(point.X, 32), 0, result, 1, 32);
            return result;
        }
        else
        {
            byte[] result = new byte[65];
            result[0] = 0x04;
            Buffer.BlockCopy(BigIntegerToBytes(point.X, 32), 0, result, 1, 32);
            Buffer.BlockCopy(BigIntegerToBytes(point.Y, 32), 0, result, 33, 32);
            return result;
        }
    }

    /// <summary>
    /// 将字节数组转换为点
    /// </summary>
    private static EcPoint BytesToPoint(byte[] data)
    {
        if (data.Length == 65 && data[0] == 0x04)
        {
            BigInteger x = new(data[1..33], true, true);
            BigInteger y = new(data[33..], true, true);
            return new EcPoint(x, y);
        }
        
        if (data.Length == 33 && (data[0] == 0x02 || data[0] == 0x03))
        {
            // 压缩格式，需要解压缩
            BigInteger x = new(data[1..], true, true);
            BigInteger y = DecompressY(x, data[0] == 0x03);
            return new EcPoint(x, y);
        }

        throw new ArgumentException("无效的公钥格式");
    }

    /// <summary>
    /// 从 x 坐标解压缩 y 坐标
    /// </summary>
    private static BigInteger DecompressY(BigInteger x, bool yIsOdd)
    {
        BigInteger y2 = Mod(x * x * x + A * x + B, P);
        BigInteger y = ModSqrt(y2, P);
        
        if (y.IsEven == yIsOdd)
            y = P - y;
        
        return y;
    }

    /// <summary>
    /// 模平方根（Tonelli-Shanks 算法）
    /// </summary>
    private static BigInteger ModSqrt(BigInteger n, BigInteger p)
    {
        // 简化的实现，假设 p ≡ 3 (mod 4)
        // 此时 sqrt(n) = n^((p+1)/4) mod p
        return BigInteger.ModPow(n, (p + 1) / 4, p);
    }

    /// <summary>
    /// 大整数转字节数组
    /// </summary>
    private static byte[] BigIntegerToBytes(BigInteger value, int length)
    {
        byte[] bytes = value.ToByteArray(true, true);
        if (bytes.Length == length)
            return bytes;

        byte[] result = new byte[length];
        int offset = length - bytes.Length;
        
        if (offset > 0)
            Buffer.BlockCopy(bytes, 0, result, offset, bytes.Length);
        else
            Buffer.BlockCopy(bytes, -offset, result, 0, length);

        return result;
    }

    /// <summary>
    /// 从十六进制字符串解析大整数
    /// </summary>
    private static BigInteger ParseHex(string hex)
    {
        return BigInteger.Parse(hex, System.Globalization.NumberStyles.HexNumber);
    }

    /// <summary>
    /// 模运算
    /// </summary>
    private static BigInteger Mod(BigInteger a, BigInteger m)
    {
        BigInteger result = a % m;
        return result < 0 ? result + m : result;
    }

    /// <summary>
    /// 模逆元
    /// </summary>
    private static BigInteger ModInverse(BigInteger a, BigInteger m)
    {
        return BigInteger.ModPow(Mod(a, m), m - 2, m);
    }

    /// <summary>
    /// 从密码派生密钥
    /// </summary>
    private static byte[] DeriveKeyFromPassword(string password)
    {
        var sm3 = new Sm3Provider();
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        byte[] hash = sm3.Hash(passwordBytes);
        
        byte[] key = new byte[16];
        Buffer.BlockCopy(hash, 0, key, 0, 16);
        return key;
    }

    #endregion
}
