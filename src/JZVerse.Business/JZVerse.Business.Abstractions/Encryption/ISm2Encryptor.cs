namespace JZVerse.Business.Abstractions.Encryption;

/// <summary>
/// SM2 加密器接口
/// </summary>
public interface ISm2Encryptor
{
    /// <summary>
    /// 加密文本
    /// </summary>
    /// <param name="plainText">明文</param>
    /// <returns>密文（格式：sm2:{Base64}）</returns>
    string Encrypt(string plainText);

    /// <summary>
    /// 解密文本
    /// </summary>
    /// <param name="cipherText">密文（格式：sm2:{Base64}）</param>
    /// <returns>明文</returns>
    string Decrypt(string cipherText);

    /// <summary>
    /// 加密字节数组
    /// </summary>
    /// <param name="plainBytes">明文字节数组</param>
    /// <returns>密文字节数组</returns>
    byte[] EncryptBytes(byte[] plainBytes);

    /// <summary>
    /// 解密字节数组
    /// </summary>
    /// <param name="cipherBytes">密文字节数组</param>
    /// <returns>明文字节数组</returns>
    byte[] DecryptBytes(byte[] cipherBytes);

    /// <summary>
    /// 判断是否为加密文本（格式：sm2:{Base64}）
    /// </summary>
    /// <param name="text">待判断的文本</param>
    /// <returns>是否为加密文本</returns>
    bool IsEncrypted(string text);

    /// <summary>
    /// 获取或解密（如果是加密文本则解密，否则原样返回）
    /// </summary>
    /// <param name="text">文本</param>
    /// <returns>解密后的文本或原文本</returns>
    string GetOrDecrypt(string text);
}
