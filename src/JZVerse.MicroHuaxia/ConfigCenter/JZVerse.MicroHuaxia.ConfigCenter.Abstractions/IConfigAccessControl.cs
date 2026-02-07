namespace JZVerse.MicroHuaxia.ConfigCenter.Abstractions;

/// <summary>
/// 配置访问控制接口（预留）
/// </summary>
public interface IConfigAccessControl
{
    /// <summary>
    /// 检查是否有读取权限
    /// </summary>
    Task<bool> CanReadAsync(
        string userId,
        string applicationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查是否有写入权限
    /// </summary>
    Task<bool> CanWriteAsync(
        string userId,
        string applicationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查是否有删除权限
    /// </summary>
    Task<bool> CanDeleteAsync(
        string userId,
        string applicationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查是否有发布权限
    /// </summary>
    Task<bool> CanPublishAsync(
        string userId,
        string applicationId,
        CancellationToken cancellationToken = default);
}
