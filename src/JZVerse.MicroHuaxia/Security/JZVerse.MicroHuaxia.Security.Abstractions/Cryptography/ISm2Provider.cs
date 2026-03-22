namespace JZVerse.MicroHuaxia.Security.Cryptography;

/// <summary>
/// SM2 国密算法接口
/// </summary>
/// <remarks>
/// SM2 是基于椭圆曲线的非对称加密算法，符合 GM/T 0003 标准
/// </remarks>
public interface ISm2Provider
{
    /// <summary>
    /// 生成 SM2 密钥对
    /// </summary>
    /// <returns>(公钥, 私钥)</returns>
    (byte[] publicKey, byte[] privateKey) GenerateKeyPair();

    /// <summary>
    /// 使用公钥加密
    /// </summary>
    /// <param name="plaintext">明文数据</param>
    /// <param name="publicKey">公钥</param>
    /// <returns>密文数据</returns>
    byte[] Encrypt(byte[] plaintext, byte[] publicKey);

    /// <summary>
    /// 使用私钥解密
    /// </summary>
    /// <param name="ciphertext">密文数据</param>
    /// <param name="privateKey">私钥</param>
    /// <returns>明文数据</returns>
    byte[] Decrypt(byte[] ciphertext, byte[] privateKey);

    /// <summary>
    /// 使用私钥签名
    /// </summary>
    /// <param name="data">待签名数据</param>
    /// <param name="privateKey">私钥</param>
    /// <returns>签名值</returns>
    byte[] Sign(byte[] data, byte[] privateKey);

    /// <summary>
    /// 使用公钥验签
    /// </summary>
    /// <param name="data">原始数据</param>
    /// <param name="signature">签名值</param>
    /// <param name="publicKey">公钥</param>
    /// <returns>验签是否通过</returns>
    bool Verify(byte[] data, byte[] signature, byte[] publicKey);

    /// <summary>
    /// 导出公钥为字符串格式
    /// </summary>
    /// <param name="publicKey">公钥字节</param>
    /// <returns>Base64 编码的公钥</returns>
    string ExportPublicKey(byte[] publicKey);

    /// <summary>
    /// 导入公钥
    /// </summary>
    /// <param name="publicKeyBase64">Base64 编码的公钥</param>
    /// <returns>公钥字节</returns>
    byte[] ImportPublicKey(string publicKeyBase64);

    /// <summary>
    /// 导出私钥为字符串格式（加密存储）
    /// </summary>
    /// <param name="privateKey">私钥字节</param>
    /// <param name="password">加密密码</param>
    /// <returns>加密后的私钥</returns>
    string ExportPrivateKey(byte[] privateKey, string? password = null);

    /// <summary>
    /// 导入私钥
    /// </summary>
    /// <param name="privateKeyEncrypted">加密后的私钥</param>
    /// <param name="password">解密密码</param>
    /// <returns>私钥字节</returns>
    byte[] ImportPrivateKey(string privateKeyEncrypted, string? password = null);
}
