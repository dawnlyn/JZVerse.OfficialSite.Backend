namespace JZVerse.MicroHuaxia.Security.Cryptography;

/// <summary>
/// SM4 国密对称加密算法接口
/// </summary>
/// <remarks>
/// SM4 是国密分组密码算法，分组长度和密钥长度均为 128 位，符合 GM/T 0002 标准
/// </remarks>
public interface ISm4Provider
{
    /// <summary>
    /// 生成随机密钥
    /// </summary>
    /// <returns>16 字节密钥</returns>
    byte[] GenerateKey();

    /// <summary>
    /// 生成随机初始化向量
    /// </summary>
    /// <returns>16 字节 IV</returns>
    byte[] GenerateIv();

    /// <summary>
    /// ECB 模式加密
    /// </summary>
    /// <param name="plaintext">明文数据</param>
    /// <param name="key">16 字节密钥</param>
    /// <returns>密文数据</returns>
    byte[] EncryptEcb(byte[] plaintext, byte[] key);

    /// <summary>
    /// ECB 模式解密
    /// </summary>
    /// <param name="ciphertext">密文数据</param>
    /// <param name="key">16 字节密钥</param>
    /// <returns>明文数据</returns>
    byte[] DecryptEcb(byte[] ciphertext, byte[] key);

    /// <summary>
    /// CBC 模式加密
    /// </summary>
    /// <param name="plaintext">明文数据</param>
    /// <param name="key">16 字节密钥</param>
    /// <param name="iv">16 字节初始化向量</param>
    /// <returns>密文数据</returns>
    byte[] EncryptCbc(byte[] plaintext, byte[] key, byte[] iv);

    /// <summary>
    /// CBC 模式解密
    /// </summary>
    /// <param name="ciphertext">密文数据</param>
    /// <param name="key">16 字节密钥</param>
    /// <param name="iv">16 字节初始化向量</param>
    /// <returns>明文数据</returns>
    byte[] DecryptCbc(byte[] ciphertext, byte[] key, byte[] iv);

    /// <summary>
    /// CTR 模式加密
    /// </summary>
    /// <param name="plaintext">明文数据</param>
    /// <param name="key">16 字节密钥</param>
    /// <param name="iv">16 字节初始化向量</param>
    /// <returns>密文数据</returns>
    byte[] EncryptCtr(byte[] plaintext, byte[] key, byte[] iv);

    /// <summary>
    /// CTR 模式解密（与加密相同）
    /// </summary>
    /// <param name="ciphertext">密文数据</param>
    /// <param name="key">16 字节密钥</param>
    /// <param name="iv">16 字节初始化向量</param>
    /// <returns>明文数据</returns>
    byte[] DecryptCtr(byte[] ciphertext, byte[] key, byte[] iv);

    /// <summary>
    /// GCM 模式加密（带认证）
    /// </summary>
    /// <param name="plaintext">明文数据</param>
    /// <param name="key">16 字节密钥</param>
    /// <param name="nonce">12 字节随机数</param>
    /// <param name="associatedData">附加认证数据（可选）</param>
    /// <returns>(密文, 认证标签)</returns>
    (byte[] ciphertext, byte[] tag) EncryptGcm(
        byte[] plaintext,
        byte[] key,
        byte[] nonce,
        byte[]? associatedData = null);

    /// <summary>
    /// GCM 模式解密（带认证）
    /// </summary>
    /// <param name="ciphertext">密文数据</param>
    /// <param name="key">16 字节密钥</param>
    /// <param name="nonce">12 字节随机数</param>
    /// <param name="tag">16 字节认证标签</param>
    /// <param name="associatedData">附加认证数据（可选）</param>
    /// <returns>明文数据，验证失败则抛出异常</returns>
    byte[] DecryptGcm(
        byte[] ciphertext,
        byte[] key,
        byte[] nonce,
        byte[] tag,
        byte[]? associatedData = null);
}

/// <summary>
/// SM4 加密模式
/// </summary>
public enum Sm4Mode
{
    /// <summary>
    /// 电子密码本模式
    /// </summary>
    ECB = 0,

    /// <summary>
    /// 密码分组链接模式
    /// </summary>
    CBC = 1,

    /// <summary>
    /// 计数器模式
    /// </summary>
    CTR = 2,

    /// <summary>
    /// 伽罗瓦计数器模式（带认证）
    /// </summary>
    GCM = 3
}
