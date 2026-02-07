using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.ConfigCenter.Abstractions;
using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ConfigCenter.Core.Repositories;

/// <summary>
/// 内存灰度发布仓储实现
/// </summary>
public class InMemoryGrayReleaseRepository : IGrayReleaseRepository
{
    private readonly ConcurrentDictionary<string, GrayRelease> _releases = new();

    // 命名空间+环境索引：{namespaceId}:{environmentId} -> Set<releaseId>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _namespaceIndex = new();
    private readonly Lock _indexLock = new();

    public Task<GrayRelease> AddAsync(GrayRelease release, CancellationToken cancellationToken = default)
    {
        _releases[release.ReleaseId] = release;
        UpdateIndex(release);
        return Task.FromResult(release);
    }

    public Task<bool> UpdateAsync(GrayRelease release, CancellationToken cancellationToken = default)
    {
        if (!_releases.ContainsKey(release.ReleaseId))
            return Task.FromResult(false);

        _releases[release.ReleaseId] = release;
        return Task.FromResult(true);
    }

    public Task<GrayRelease?> GetByIdAsync(string releaseId, CancellationToken cancellationToken = default)
    {
        _releases.TryGetValue(releaseId, out var release);
        return Task.FromResult(release);
    }

    public Task<IReadOnlyList<GrayRelease>> GetActiveReleasesAsync(
        string namespaceId,
        string environmentId,
        CancellationToken cancellationToken = default)
    {
        var indexKey = BuildIndexKey(namespaceId, environmentId);
        if (_namespaceIndex.TryGetValue(indexKey, out var releaseIds))
        {
            var activeReleases = releaseIds.Keys
                .Select(id => _releases.GetValueOrDefault(id))
                .Where(r => r is not null && r.Status == GrayReleaseStatus.InProgress)
                .Cast<GrayRelease>()
                .ToList();
            return Task.FromResult<IReadOnlyList<GrayRelease>>(activeReleases);
        }
        return Task.FromResult<IReadOnlyList<GrayRelease>>([]);
    }

    public Task<IReadOnlyList<GrayRelease>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<GrayRelease>>(_releases.Values.ToList());
    }

    private void UpdateIndex(GrayRelease release)
    {
        lock (_indexLock)
        {
            var indexKey = BuildIndexKey(release.NamespaceId, release.EnvironmentId);
            var releaseSet = _namespaceIndex.GetOrAdd(indexKey, _ => new ConcurrentDictionary<string, byte>());
            releaseSet[release.ReleaseId] = 0;
        }
    }

    private static string BuildIndexKey(string namespaceId, string environmentId)
        => $"{namespaceId}:{environmentId}";
}
