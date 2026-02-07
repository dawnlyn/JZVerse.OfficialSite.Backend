using JZVerse.MicroHuaxia.ConfigCenter.Abstractions;
using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.ConfigCenter.Core.Services;

/// <summary>
/// 配置发现服务实现
/// </summary>
public class ConfigDiscoveryService(
    IConfigItemRepository _repository,
    IConfigCache _cache,
    IGrayReleaseManager _grayReleaseManager,
    ILogger<ConfigDiscoveryService> _logger
) : IConfigDiscovery
{
    public async Task<Dictionary<string, string>> GetConfigAsync(
        ConfigQuery query,
        CancellationToken cancellationToken = default)
    {
        var items = await QueryAsync(query, cancellationToken);
        return items.ToDictionary(i => i.Key, i => i.Value);
    }

    public async Task<string?> GetValueAsync(
        string applicationId,
        string environmentId,
        string key,
        CancellationToken cancellationToken = default)
    {
        // 从所有命名空间中查找 key
        var allItems = await _repository.GetAllAsync(cancellationToken);
        var item = allItems.FirstOrDefault(i =>
            i.EnvironmentId == environmentId && i.Key == key);

        return item?.Value;
    }

    public Task<IReadOnlyList<ConfigItem>> QueryAsync(
        ConfigQuery query,
        CancellationToken cancellationToken = default)
    {
        return _repository.QueryAsync(query, cancellationToken);
    }

    public async Task<Dictionary<string, string>> GetNamespaceConfigAsync(
        string namespaceId,
        string environmentId,
        ClientInfo? clientInfo = null,
        CancellationToken cancellationToken = default)
    {
        // 尝试从缓存获取
        var cacheKey = BuildCacheKey(namespaceId, environmentId, clientInfo);
        if (_cache.IsCacheEnabled)
        {
            var cached = await _cache.GetAsync(cacheKey, cancellationToken);
            if (cached is not null)
            {
                _logger.LogDebug("Cache hit for namespace {NamespaceId} env {EnvironmentId}",
                    namespaceId, environmentId);
                return cached;
            }
        }

        // 检查是否有进行中的灰度发布
        if (clientInfo is not null)
        {
            var activeReleases = await _grayReleaseManager.GetActiveReleasesAsync(
                namespaceId, environmentId, cancellationToken);

            foreach (var release in activeReleases)
            {
                var matches = await _grayReleaseManager.MatchesGrayRuleAsync(
                    release.ReleaseId, clientInfo, cancellationToken);

                if (matches)
                {
                    _logger.LogInformation(
                        "Client {ClientId} matches gray release {ReleaseId}, returning gray config",
                        clientInfo.ClientId, release.ReleaseId);

                    // 返回灰度配置快照
                    if (_cache.IsCacheEnabled)
                    {
                        await _cache.SetAsync(cacheKey, release.ConfigSnapshot, cancellationToken: cancellationToken);
                    }
                    return release.ConfigSnapshot;
                }
            }
        }

        // 从仓储获取正式配置
        var items = await _repository.GetByNamespaceAsync(namespaceId, environmentId, cancellationToken);
        var config = items.ToDictionary(i => i.Key, i => i.Value);

        // 写入缓存
        if (_cache.IsCacheEnabled)
        {
            await _cache.SetAsync(cacheKey, config, cancellationToken: cancellationToken);
        }

        _logger.LogDebug(
            "Loaded {Count} config items for namespace {NamespaceId} env {EnvironmentId}",
            config.Count, namespaceId, environmentId);

        return config;
    }

    private static string BuildCacheKey(string namespaceId, string environmentId, ClientInfo? clientInfo)
    {
        var baseKey = $"config:ns:{namespaceId}:{environmentId}";
        if (clientInfo is not null)
        {
            return $"{baseKey}:{clientInfo.ClientId}";
        }
        return baseKey;
    }
}
