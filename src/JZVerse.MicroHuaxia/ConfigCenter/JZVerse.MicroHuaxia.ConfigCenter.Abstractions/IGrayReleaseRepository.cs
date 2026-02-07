using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ConfigCenter.Abstractions;

/// <summary>
/// 灰度发布仓储接口
/// </summary>
public interface IGrayReleaseRepository
{
    /// <summary>
    /// 添加灰度发布
    /// </summary>
    Task<GrayRelease> AddAsync(GrayRelease release, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新灰度发布
    /// </summary>
    Task<bool> UpdateAsync(GrayRelease release, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取灰度发布详情
    /// </summary>
    Task<GrayRelease?> GetByIdAsync(string releaseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取命名空间进行中的灰度发布
    /// </summary>
    Task<IReadOnlyList<GrayRelease>> GetActiveReleasesAsync(
        string namespaceId,
        string environmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有灰度发布
    /// </summary>
    Task<IReadOnlyList<GrayRelease>> GetAllAsync(CancellationToken cancellationToken = default);
}
