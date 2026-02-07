using System.Security.Cryptography;
using System.Text;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Authentication;

namespace JZVerse.MicroHuaxia.Gateway.Core.Authentication;

/// <summary>
/// AES 密钥加密器实现
/// </summary>
public sealed class AesSecretEncryptor : ISecretEncryptor
{
    private const string EncryptedPrefix = "encrypted:";
    private readonly byte[] _key;
    private readonly byte[] _iv;

    public AesSecretEncryptor(string masterKey)
    {
        if (string.IsNullOrEmpty(masterKey))
        {
            throw new ArgumentException("主密钥不能为空", nameof(masterKey));
        }

        // 从主密钥派生 AES 密钥和 IV
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(masterKey));
        _key = hash[..32]; // AES-256 需要 32 字节密钥
        _iv = hash[..16];  // IV 需要 16 字节
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return plainText;
        }

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        return EncryptedPrefix + Convert.ToBase64String(encryptedBytes);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText) || !IsEncrypted(cipherText))
        {
            throw new ArgumentException("无效的加密文本格式", nameof(cipherText));
        }

        var encryptedBase64 = cipherText[EncryptedPrefix.Length..];
        var encryptedBytes = Convert.FromBase64String(encryptedBase64);

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);

        return Encoding.UTF8.GetString(plainBytes);
    }

    public bool IsEncrypted(string text)
    {
        return !string.IsNullOrEmpty(text) && text.StartsWith(EncryptedPrefix, StringComparison.Ordinal);
    }

    public string GetOrDecrypt(string text)
    {
        return IsEncrypted(text) ? Decrypt(text) : text;
    }
}

/// <summary>
/// 空操作密钥加密器（不加密，用于开发环境）
/// </summary>
public sealed class NoOpSecretEncryptor : ISecretEncryptor
{
    private const string EncryptedPrefix = "encrypted:";

    public string Encrypt(string plainText)
    {
        return EncryptedPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));
    }

    public string Decrypt(string cipherText)
    {
        if (!IsEncrypted(cipherText))
        {
            throw new ArgumentException("无效的加密文本格式", nameof(cipherText));
        }

        var base64 = cipherText[EncryptedPrefix.Length..];
        return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
    }

    public bool IsEncrypted(string text)
    {
        return !string.IsNullOrEmpty(text) && text.StartsWith(EncryptedPrefix, StringComparison.Ordinal);
    }

    public string GetOrDecrypt(string text)
    {
        return IsEncrypted(text) ? Decrypt(text) : text;
    }
}
