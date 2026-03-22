namespace JZVerse.MicroHuaxia.Security.Secrets;

/// <summary>
/// 密钥存储接口
/// </summary>
public interface ISecretStore
{
    /// <summary>
    /// 获取密钥
    /// </summary>
    /// <param name="secretId">密钥 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>密钥信息</returns>
    Task<Secret?> GetSecretAsync(
        string secretId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据名称获取密钥
    /// </summary>
    /// <param name="name">密钥名称</param>
    /// <param name="namespace">命名空间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>密钥信息</returns>
    Task<Secret?> GetSecretByNameAsync(
        string name,
        string? @namespace = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取密钥值（解密后）
    /// </summary>
    /// <param name="secretId">密钥 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>密钥值</returns>
    Task<string?> GetSecretValueAsync(
        string secretId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建密钥
    /// </summary>
    /// <param name="name">密钥名称</param>
    /// <param name="value">密钥值</param>
    /// <param name="type">密钥类型</param>
    /// <param name="namespace">命名空间</param>
    /// <param name="description">描述</param>
    /// <param name="expiresAt">过期时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>创建的密钥</returns>
    Task<Secret> CreateSecretAsync(
        string name,
        string value,
        SecretType type = SecretType.Custom,
        string? @namespace = null,
        string? description = null,
        DateTimeOffset? expiresAt = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新密钥值
    /// </summary>
    /// <param name="secretId">密钥 ID</param>
    /// <param name="newValue">新密钥值</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的密钥</returns>
    Task<Secret> UpdateSecretAsync(
        string secretId,
        string newValue,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除密钥
    /// </summary>
    /// <param name="secretId">密钥 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> DeleteSecretAsync(
        string secretId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 轮换密钥
    /// </summary>
    /// <param name="secretId">密钥 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>轮换后的密钥</returns>
    Task<Secret> RotateSecretAsync(
        string secretId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取需要轮换的密钥列表
    /// </summary>
    /// <param name="threshold">轮换阈值（提前多少天）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>密钥列表</returns>
    Task<IReadOnlyList<Secret>> GetSecretsNeedRotationAsync(
        TimeSpan threshold,
        CancellationToken cancellationToken = default);
}
