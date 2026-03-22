using System.Text;
using JZVerse.Business.Abstractions.Encryption;
using JZVerse.MicroHuaxia.Security;
using JZVerse.MicroHuaxia.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace JZVerse.Business.Infrastructure.Encryption;

/// <summary>
/// SM2 加密器实现（基于国密 SM2 椭圆曲线算法）
/// </summary>
/// <remarks>
/// 此实现作为 Security 模块 ISm2Provider 的适配器，提供业务层兼容接口。
/// 底层加密逻辑已迁移至 JZVerse.MicroHuaxia.Security.Core.Cryptography.Sm2Provider
/// </remarks>
public sealed class Sm2Encryptor : ISm2Encryptor, IDisposable
{
    private const string Sm2Prefix = "sm2:";

    private readonly Sm2Options _options;
    private readonly ISm2Provider _sm2Provider;
    private readonly byte[]? _publicKey;
    private readonly byte[]? _privateKey;

    public Sm2Encryptor(
        IOptions<Sm2Options> options,
        ISm2Provider sm2Provider)
    {
        _options = options.Value;
        _sm2Provider = sm2Provider;

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
        var cipherBytes = _sm2Provider.Encrypt(plainBytes, _publicKey);
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
        var plainBytes = _sm2Provider.Decrypt(cipherBytes, _privateKey);
        return Encoding.UTF8.GetString(plainBytes);
    }

    /// <inheritdoc />
    public byte[] EncryptBytes(byte[] plainBytes)
    {
        if (!_options.Enabled || _publicKey is null)
            throw new InvalidOperationException("SM2 加密未启用或公钥未配置");

        return _sm2Provider.Encrypt(plainBytes, _publicKey);
    }

    /// <inheritdoc />
    public byte[] DecryptBytes(byte[] cipherBytes)
    {
        if (!_options.Enabled || _privateKey is null)
            throw new InvalidOperationException("SM2 解密未启用或私钥未配置");

        return _sm2Provider.Decrypt(cipherBytes, _privateKey);
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

    /// <summary>
    /// 生成 SM2 密钥对
    /// </summary>
    /// <returns>(公钥 Base64, 私钥 Base64)</returns>
    public (string publicKey, string privateKey) GenerateKeyPair()
    {
        // 使用 Security 模块的密钥生成
        var (publicKey, privateKey) = _sm2Provider.GenerateKeyPair();
        return (Convert.ToBase64String(publicKey), Convert.ToBase64String(privateKey));
    }

    public void Dispose()
    {
        // ISm2Provider 不由本类管理生命周期
    }
}
