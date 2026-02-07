using JZVerse.MicroHuaxia.ConfigCenter.Abstractions;
using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;
using JZVerse.MicroHuaxia.ConfigCenter.Core.GrayReleases;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.ConfigCenter.Core.Services;

/// <summary>
/// 灰度发布管理器实现
/// </summary>
public class GrayReleaseManager(
    IGrayReleaseRepository _repository,
    IConfigItemRepository _configRepository,
    IConfigEventPublisher _eventPublisher,
    ILogger<GrayReleaseManager> _logger
) : IGrayReleaseManager
{
    public async Task<GrayRelease> CreateReleaseAsync(
        GrayRelease release,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating gray release {ReleaseName} for namespace {NamespaceId}",
            release.ReleaseName, release.NamespaceId);

        // 创建配置快照
        var items = await _configRepository.GetByNamespaceAsync(
            release.NamespaceId, release.EnvironmentId, cancellationToken);

        var savedRelease = release with
        {
            ReleaseId = string.IsNullOrEmpty(release.ReleaseId)
                ? Guid.NewGuid().ToString("N")
                : release.ReleaseId,
            Status = GrayReleaseStatus.Draft,
            ConfigSnapshot = items.ToDictionary(i => i.Key, i => i.Value),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await _repository.AddAsync(savedRelease, cancellationToken);

        _logger.LogInformation(
            "Created gray release {ReleaseId} with {ConfigCount} config items",
            savedRelease.ReleaseId, savedRelease.ConfigSnapshot.Count);

        return savedRelease;
    }

    public async Task<bool> StartReleaseAsync(string releaseId, CancellationToken cancellationToken = default)
    {
        var release = await _repository.GetByIdAsync(releaseId, cancellationToken);
        if (release is null)
        {
            _logger.LogWarning("Gray release {ReleaseId} not found", releaseId);
            return false;
        }

        if (release.Status != GrayReleaseStatus.Draft)
        {
            _logger.LogWarning(
                "Cannot start gray release {ReleaseId}, current status: {Status}",
                releaseId, release.Status);
            return false;
        }

        release.Status = GrayReleaseStatus.InProgress;
        release.StartedAt = DateTimeOffset.UtcNow;

        await _repository.UpdateAsync(release, cancellationToken);

        _logger.LogInformation("Started gray release {ReleaseId}", releaseId);

        // 发布事件
        await PublishGrayReleaseEventAsync(release, ConfigEventType.GrayReleaseStarted, cancellationToken);

        return true;
    }

    public async Task<bool> CompleteReleaseAsync(string releaseId, CancellationToken cancellationToken = default)
    {
        var release = await _repository.GetByIdAsync(releaseId, cancellationToken);
        if (release is null)
        {
            _logger.LogWarning("Gray release {ReleaseId} not found", releaseId);
            return false;
        }

        if (release.Status != GrayReleaseStatus.InProgress)
        {
            _logger.LogWarning(
                "Cannot complete gray release {ReleaseId}, current status: {Status}",
                releaseId, release.Status);
            return false;
        }

        release.Status = GrayReleaseStatus.Completed;
        release.CompletedAt = DateTimeOffset.UtcNow;

        await _repository.UpdateAsync(release, cancellationToken);

        _logger.LogInformation("Completed gray release {ReleaseId}", releaseId);

        // 发布事件
        await PublishGrayReleaseEventAsync(release, ConfigEventType.GrayReleaseCompleted, cancellationToken);

        return true;
    }

    public async Task<bool> RollbackReleaseAsync(string releaseId, CancellationToken cancellationToken = default)
    {
        var release = await _repository.GetByIdAsync(releaseId, cancellationToken);
        if (release is null)
        {
            _logger.LogWarning("Gray release {ReleaseId} not found", releaseId);
            return false;
        }

        if (release.Status != GrayReleaseStatus.InProgress)
        {
            _logger.LogWarning(
                "Cannot rollback gray release {ReleaseId}, current status: {Status}",
                releaseId, release.Status);
            return false;
        }

        release.Status = GrayReleaseStatus.Rollback;
        release.CompletedAt = DateTimeOffset.UtcNow;

        await _repository.UpdateAsync(release, cancellationToken);

        _logger.LogInformation("Rolled back gray release {ReleaseId}", releaseId);

        // 发布事件
        await PublishGrayReleaseEventAsync(release, ConfigEventType.Rollback, cancellationToken);

        return true;
    }

    public Task<GrayRelease?> GetReleaseAsync(string releaseId, CancellationToken cancellationToken = default)
    {
        return _repository.GetByIdAsync(releaseId, cancellationToken);
    }

    public async Task<bool> MatchesGrayRuleAsync(
        string releaseId,
        ClientInfo clientInfo,
        CancellationToken cancellationToken = default)
    {
        var release = await _repository.GetByIdAsync(releaseId, cancellationToken);
        if (release is null || release.Status != GrayReleaseStatus.InProgress)
        {
            return false;
        }

        return GrayRuleEvaluator.Matches(release.TargetRules, clientInfo);
    }

    public Task<IReadOnlyList<GrayRelease>> GetActiveReleasesAsync(
        string namespaceId,
        string environmentId,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetActiveReleasesAsync(namespaceId, environmentId, cancellationToken);
    }

    private async Task PublishGrayReleaseEventAsync(
        GrayRelease release,
        ConfigEventType eventType,
        CancellationToken cancellationToken)
    {
        var @event = new ConfigChangeEvent
        {
            EventId = Guid.NewGuid().ToString("N"),
            EventType = eventType,
            ApplicationId = string.Empty,
            EnvironmentId = release.EnvironmentId,
            NamespaceId = release.NamespaceId,
            ChangedItems = [],
            Timestamp = DateTimeOffset.UtcNow,
            TriggeredBy = release.CreatedBy,
            AdditionalData = new Dictionary<string, object>
            {
                ["releaseId"] = release.ReleaseId,
                ["releaseName"] = release.ReleaseName,
                ["status"] = release.Status.ToString(),
            },
        };

        await _eventPublisher.PublishAsync(@event, cancellationToken);
    }
}
