using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ConfigCenter.Abstractions;

/// <summary>
/// 灰度发布管理接口
/// </summary>
public interface IGrayReleaseManager
{
    /// <summary>
    /// 创建灰度发布
    /// </summary>
    Task<GrayRelease> CreateReleaseAsync(
        GrayRelease release,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 开始灰度发布
    /// </summary>
    Task<bool> StartReleaseAsync(
        string releaseId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 完成灰度发布
    /// </summary>
    Task<bool> CompleteReleaseAsync(
        string releaseId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 回滚灰度发布
    /// </summary>
    Task<bool> RollbackReleaseAsync(
        string releaseId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取灰度发布详情
    /// </summary>
    Task<GrayRelease?> GetReleaseAsync(
        string releaseId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 判断客户端是否匹配灰度规则
    /// </summary>
    Task<bool> MatchesGrayRuleAsync(
        string releaseId,
        ClientInfo clientInfo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取命名空间当前进行中的灰度发布
    /// </summary>
    Task<IReadOnlyList<GrayRelease>> GetActiveReleasesAsync(
        string namespaceId,
        string environmentId,
        CancellationToken cancellationToken = default);
}
