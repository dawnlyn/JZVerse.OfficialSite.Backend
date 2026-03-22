namespace JZVerse.MicroHuaxia.Security.Cryptography;

/// <summary>
/// SM3 国密哈希算法接口
/// </summary>
/// <remarks>
/// SM3 是国密密码杂凑算法，输出 256 位哈希值，符合 GM/T 0004 标准
/// </remarks>
public interface ISm3Provider
{
    /// <summary>
    /// 计算哈希值
    /// </summary>
    /// <param name="data">输入数据</param>
    /// <returns>32 字节哈希值</returns>
    byte[] Hash(byte[] data);

    /// <summary>
    /// 计算哈希值（字符串输入）
    /// </summary>
    /// <param name="input">输入字符串</param>
    /// <returns>32 字节哈希值</returns>
    byte[] Hash(string input);

    /// <summary>
    /// 计算哈希值并返回十六进制字符串
    /// </summary>
    /// <param name="data">输入数据</param>
    /// <returns>64 字符十六进制字符串</returns>
    string HashToHex(byte[] data);

    /// <summary>
    /// 计算 HMAC-SM3
    /// </summary>
    /// <param name="data">输入数据</param>
    /// <param name="key">密钥</param>
    /// <returns>HMAC 值</returns>
    byte[] Hmac(byte[] data, byte[] key);

    /// <summary>
    /// 增量式哈希计算
    /// </summary>
    /// <returns>哈希计算器实例</returns>
    ISm3HashAlgorithm CreateHashAlgorithm();
}

/// <summary>
/// SM3 哈希算法实例接口
/// </summary>
public interface ISm3HashAlgorithm
{
    /// <summary>
    /// 追加数据块
    /// </summary>
    /// <param name="data">数据块</param>
    void AppendData(byte[] data);

    /// <summary>
    /// 追加数据块
    /// </summary>
    /// <param name="data">数据块</param>
    /// <param name="offset">偏移量</param>
    /// <param name="count">长度</param>
    void AppendData(byte[] data, int offset, int count);

    /// <summary>
    /// 获取最终哈希值
    /// </summary>
    /// <returns>32 字节哈希值</returns>
    byte[] GetHashAndReset();

    /// <summary>
    /// 重置状态
    /// </summary>
    void Reset();
}
