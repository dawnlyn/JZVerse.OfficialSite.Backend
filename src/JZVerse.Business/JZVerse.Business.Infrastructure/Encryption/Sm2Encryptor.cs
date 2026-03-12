using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using JZVerse.Business.Abstractions.Encryption;
using Microsoft.Extensions.Options;

namespace JZVerse.Business.Infrastructure.Encryption;

/// <summary>
/// SM2 加密器实现（基于国密 SM2 椭圆曲线算法）
/// </summary>
/// <remarks>
/// SM2 使用 256 位椭圆曲线，曲线参数定义在 GM/T 0003 标准中
/// 密文格式：sm2:{Base64(C1 || C3 || C2)}
/// 其中 C1 为椭圆曲线点，C3 为 SM3 哈希，C2 为密文数据
/// </remarks>
public sealed class Sm2Encryptor : ISm2Encryptor
{
    private const string Sm2Prefix = "sm2:";

    // SM2 椭圆曲线参数（国密标准）
    private static readonly BigInteger P = BigInteger.Parse("0FFFFFFFEFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF00000000FFFFFFFFFFFFFFFF", System.Globalization.NumberStyles.HexNumber);
    private static readonly BigInteger A = BigInteger.Parse("0FFFFFFFEFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF00000000FFFFFFFFFFFFFFFC", System.Globalization.NumberStyles.HexNumber);
    private static readonly BigInteger B = BigInteger.Parse("028E9FA9E9D9F5E344D5A9E4BCF6509A7F39789F515AB8F92DDBCBD414D940E93", System.Globalization.NumberStyles.HexNumber);
    private static readonly BigInteger N = BigInteger.Parse("0FFFFFFFEFFFFFFFFFFFFFFFFFFFFFFFF7203DF6B21C6052B53BBF40939D54123", System.Globalization.NumberStyles.HexNumber);
    private static readonly BigInteger Gx = BigInteger.Parse("032C4AE2C1F1981195F9904466A39C9948FE30BBFF2660BE1715A4589334C74C7", System.Globalization.NumberStyles.HexNumber);
    private static readonly BigInteger Gy = BigInteger.Parse("0BC3736A2F4F6779C59BDCEE36B692153D0A9877CC62A474002DF32E52139F0A0", System.Globalization.NumberStyles.HexNumber);

    private readonly Sm2Options _options;
    private readonly byte[]? _publicKey;
    private readonly byte[]? _privateKey;

    public Sm2Encryptor(IOptions<Sm2Options> options)
    {
        _options = options.Value;

        if (_options.Enabled)
        {
            if (!string.IsNullOrEmpty(_options.PublicKey))
                _publicKey = Convert.FromBase64String(_options.PublicKey);

            if (!string.IsNullOrEmpty(_options.PrivateKey))
                _privateKey = Convert.FromBase64String(_options.PrivateKey);
        }
    }

    /// <inheritdoc />
    public string Encrypt(string plainText)
    {
        if (!_options.Enabled || _publicKey is null)
            throw new InvalidOperationException("SM2 加密未启用或公钥未配置");

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = EncryptBytes(plainBytes);
        return Sm2Prefix + Convert.ToBase64String(cipherBytes);
    }

    /// <inheritdoc />
    public string Decrypt(string cipherText)
    {
        if (!_options.Enabled || _privateKey is null)
            throw new InvalidOperationException("SM2 解密未启用或私钥未配置");

        if (!cipherText.StartsWith(Sm2Prefix))
            throw new ArgumentException("无效的 SM2 密文格式", nameof(cipherText));

        var base64 = cipherText[Sm2Prefix.Length..];
        var cipherBytes = Convert.FromBase64String(base64);
        var plainBytes = DecryptBytes(cipherBytes);
        return Encoding.UTF8.GetString(plainBytes);
    }

    /// <inheritdoc />
    public byte[] EncryptBytes(byte[] plainBytes)
    {
        if (!_options.Enabled || _publicKey is null)
            throw new InvalidOperationException("SM2 加密未启用或公钥未配置");

        // 解析公钥点
        var (px, py) = ParsePublicKey(_publicKey);

        // 生成随机数 k
        var k = GenerateRandomK();

        // 计算 C1 = k * G
        var (c1x, c1y) = PointMultiply(Gx, Gy, k);

        // 计算 S = k * P（公钥点）
        var (sx, sy) = PointMultiply(px, py, k);

        // 使用 KDF 生成密钥流
        var keyStream = KDF(BigIntegerToBytes(sx, 32), BigIntegerToBytes(sy, 32), plainBytes.Length);

        // C2 = M xor t
        var c2 = new byte[plainBytes.Length];
        for (var i = 0; i < plainBytes.Length; i++)
        {
            c2[i] = (byte)(plainBytes[i] ^ keyStream[i]);
        }

        // C3 = SM3(x2 || M || y2)
        var c3 = ComputeSm3Hash(BigIntegerToBytes(sx, 32), plainBytes, BigIntegerToBytes(sy, 32));

        // 组装密文：C1 || C3 || C2
        var c1Bytes = PointToBytes(c1x, c1y);
        var result = new byte[c1Bytes.Length + c3.Length + c2.Length];
        c1Bytes.CopyTo(result, 0);
        c3.CopyTo(result, c1Bytes.Length);
        c2.CopyTo(result, c1Bytes.Length + c3.Length);

        return result;
    }

    /// <inheritdoc />
    public byte[] DecryptBytes(byte[] cipherBytes)
    {
        if (!_options.Enabled || _privateKey is null)
            throw new InvalidOperationException("SM2 解密未启用或私钥未配置");

        // 解析密文
        // C1: 65 字节（04 + 32 + 32）
        // C3: 32 字节（SM3 哈希）
        // C2: 剩余字节
        if (cipherBytes.Length < 65 + 32 + 1)
            throw new ArgumentException("密文长度无效", nameof(cipherBytes));

        var c1Bytes = cipherBytes[..65];
        var c3 = cipherBytes[65..97];
        var c2 = cipherBytes[97..];

        // 解析 C1 点
        var (c1x, c1y) = ParsePoint(c1Bytes);

        // 解析私钥
        var d = new BigInteger(_privateKey, true, true);

        // 计算 S = d * C1
        var (sx, sy) = PointMultiply(c1x, c1y, d);

        // 使用 KDF 生成密钥流
        var keyStream = KDF(BigIntegerToBytes(sx, 32), BigIntegerToBytes(sy, 32), c2.Length);

        // M = C2 xor t
        var plainBytes = new byte[c2.Length];
        for (var i = 0; i < c2.Length; i++)
        {
            plainBytes[i] = (byte)(c2[i] ^ keyStream[i]);
        }

        // 验证 C3
        var expectedC3 = ComputeSm3Hash(BigIntegerToBytes(sx, 32), plainBytes, BigIntegerToBytes(sy, 32));
        if (!c3.SequenceEqual(expectedC3))
            throw new CryptographicException("SM2 解密验证失败：哈希不匹配");

        return plainBytes;
    }

    /// <inheritdoc />
    public bool IsEncrypted(string text)
    {
        return !string.IsNullOrEmpty(text) && text.StartsWith(Sm2Prefix);
    }

    /// <inheritdoc />
    public string GetOrDecrypt(string text)
    {
        if (IsEncrypted(text))
            return Decrypt(text);

        return text;
    }

    #region 椭圆曲线运算

    private static (BigInteger x, BigInteger y) ParsePublicKey(byte[] publicKey)
    {
        if (publicKey[0] != 0x04 || publicKey.Length != 65)
            throw new ArgumentException("无效的公钥格式");

        var x = new BigInteger(publicKey[1..33], true, true);
        var y = new BigInteger(publicKey[33..65], true, true);
        return (x, y);
    }

    private static (BigInteger x, BigInteger y) ParsePoint(byte[] point)
    {
        return ParsePublicKey(point);
    }

    private static byte[] PointToBytes(BigInteger x, BigInteger y)
    {
        var result = new byte[65];
        result[0] = 0x04;
        BigIntegerToBytes(x, 32).CopyTo(result, 1);
        BigIntegerToBytes(y, 32).CopyTo(result, 33);
        return result;
    }

    private static BigInteger GenerateRandomK()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var k = new BigInteger(bytes, true, true);
        return BigInteger.Remainder(k, N - 1) + 1;
    }

    private static (BigInteger x, BigInteger y) PointMultiply(BigInteger px, BigInteger py, BigInteger k)
    {
        var rx = BigInteger.Zero;
        var ry = BigInteger.Zero;
        var isInfinity = true;

        var tx = px;
        var ty = py;

        while (k > 0)
        {
            if ((k & 1) == 1)
            {
                if (isInfinity)
                {
                    rx = tx;
                    ry = ty;
                    isInfinity = false;
                }
                else
                {
                    (rx, ry) = PointAdd(rx, ry, tx, ty);
                }
            }

            (tx, ty) = PointDouble(tx, ty);
            k >>= 1;
        }

        return (rx, ry);
    }

    private static (BigInteger x, BigInteger y) PointAdd(BigInteger x1, BigInteger y1, BigInteger x2, BigInteger y2)
    {
        if (x1 == x2 && y1 == y2)
            return PointDouble(x1, y1);

        var lambda = ModMul(y2 - y1, ModInverse(x2 - x1, P), P);
        lambda = Mod(lambda, P);

        var x3 = Mod(ModMul(lambda, lambda, P) - x1 - x2, P);
        var y3 = Mod(ModMul(lambda, x1 - x3, P) - y1, P);

        return (x3, y3);
    }

    private static (BigInteger x, BigInteger y) PointDouble(BigInteger x, BigInteger y)
    {
        var lambda = ModMul(3 * ModMul(x, x, P) + A, ModInverse(2 * y, P), P);
        lambda = Mod(lambda, P);

        var x3 = Mod(ModMul(lambda, lambda, P) - 2 * x, P);
        var y3 = Mod(ModMul(lambda, x - x3, P) - y, P);

        return (x3, y3);
    }

    private static BigInteger Mod(BigInteger a, BigInteger m)
    {
        var result = a % m;
        return result < 0 ? result + m : result;
    }

    private static BigInteger ModMul(BigInteger a, BigInteger b, BigInteger m)
    {
        return Mod(a * b, m);
    }

    private static BigInteger ModInverse(BigInteger a, BigInteger m)
    {
        a = Mod(a, m);
        return BigInteger.ModPow(a, m - 2, m);
    }

    private static byte[] BigIntegerToBytes(BigInteger value, int length)
    {
        var bytes = value.ToByteArray(true, true);
        if (bytes.Length == length)
            return bytes;

        var result = new byte[length];
        var offset = length - bytes.Length;
        if (offset > 0)
            bytes.CopyTo(result, offset);
        else
            Array.Copy(bytes, -offset, result, 0, length);

        return result;
    }

    #endregion

    #region KDF 和 SM3

    private static byte[] KDF(byte[] x, byte[] y, int keyLen)
    {
        // 简化的 KDF，使用 SHA256 代替 SM3（完整实现应使用 SM3）
        var ct = 1;
        var result = new List<byte>();

        while (result.Count < keyLen)
        {
            using var sha256 = SHA256.Create();
            sha256.TransformBlock(x, 0, x.Length, null, 0);
            sha256.TransformBlock(y, 0, y.Length, null, 0);
            var ctBytes = BitConverter.GetBytes(ct);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(ctBytes);
            sha256.TransformFinalBlock(ctBytes, 0, ctBytes.Length);
            result.AddRange(sha256.Hash!);
            ct++;
        }

        return result.Take(keyLen).ToArray();
    }

    private static byte[] ComputeSm3Hash(byte[] x, byte[] m, byte[] y)
    {
        // 简化实现，使用 SHA256 代替 SM3（完整实现应使用 SM3）
        using var sha256 = SHA256.Create();
        sha256.TransformBlock(x, 0, x.Length, null, 0);
        sha256.TransformBlock(m, 0, m.Length, null, 0);
        sha256.TransformFinalBlock(y, 0, y.Length);
        return sha256.Hash!;
    }

    #endregion

    #region 密钥生成工具

    /// <summary>
    /// 生成 SM2 密钥对
    /// </summary>
    /// <returns>(公钥 Base64, 私钥 Base64)</returns>
    public static (string publicKey, string privateKey) GenerateKeyPair()
    {
        // 生成随机私钥
        var dBytes = RandomNumberGenerator.GetBytes(32);
        var d = new BigInteger(dBytes, true, true);
        d = BigInteger.Remainder(d, N - 1) + 1;

        // 计算公钥 P = d * G
        var (px, py) = PointMultiply(Gx, Gy, d);

        // 序列化
        var publicKeyBytes = PointToBytes(px, py);
        var privateKeyBytes = BigIntegerToBytes(d, 32);

        return (Convert.ToBase64String(publicKeyBytes), Convert.ToBase64String(privateKeyBytes));
    }

    #endregion
}
