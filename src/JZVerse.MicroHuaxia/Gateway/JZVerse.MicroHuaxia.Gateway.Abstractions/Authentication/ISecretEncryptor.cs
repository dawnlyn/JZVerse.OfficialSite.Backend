namespace JZVerse.MicroHuaxia.Gateway.Abstractions.Authentication;

/// <summary>
/// 密钥加密器接口
/// </summary>
public interface ISecretEncryptor
{
    /// <summary>
    /// 加密纯文本
    /// </summary>
    /// <param name="plainText">纯文本</param>
    /// <returns>加密后的文本（格式：encrypted:xxxxx）</returns>
    string Encrypt(string plainText);

    /// <summary>
    /// 解密加密文本
    /// </summary>
    /// <param name="cipherText">加密文本</param>
    /// <returns>解密后的纯文本</returns>
    string Decrypt(string cipherText);

    /// <summary>
    /// 判断是否是加密文本
    /// </summary>
    /// <param name="text">文本</param>
    bool IsEncrypted(string text);

    /// <summary>
    /// 获取或解密文本（如果是加密文本则解密，否则原样返回）
    /// </summary>
    /// <param name="text">文本</param>
    string GetOrDecrypt(string text);
}

/// <summary>
/// 认证策略存储接口
/// </summary>
public interface IAuthenticationStrategyRepository
{
    /// <summary>
    /// 获取所有认证策略
    /// </summary>
    Task<IReadOnlyList<AuthenticationStrategy>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定认证策略
    /// </summary>
    /// <param name="name">策略名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<AuthenticationStrategy?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// 添加认证策略
    /// </summary>
    Task AddAsync(AuthenticationStrategy strategy, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新认证策略
    /// </summary>
    Task UpdateAsync(AuthenticationStrategy strategy, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除认证策略
    /// </summary>
    Task<bool> DeleteAsync(string name, CancellationToken cancellationToken = default);
}
