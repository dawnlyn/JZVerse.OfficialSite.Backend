using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ConfigCenter.Abstractions;

/// <summary>
/// 配置版本仓储接口
/// </summary>
public interface IConfigVersionRepository
{
    /// <summary>
    /// 添加版本记录
    /// </summary>
    Task<ConfigVersion> AddAsync(ConfigVersion version, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取配置项的版本历史
    /// </summary>
    Task<IReadOnlyList<ConfigVersion>> GetByItemIdAsync(
        string itemId,
        int limit = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定版本详情
    /// </summary>
    Task<ConfigVersion?> GetByVersionAsync(
        string itemId,
        long version,
        CancellationToken cancellationToken = default);
}
