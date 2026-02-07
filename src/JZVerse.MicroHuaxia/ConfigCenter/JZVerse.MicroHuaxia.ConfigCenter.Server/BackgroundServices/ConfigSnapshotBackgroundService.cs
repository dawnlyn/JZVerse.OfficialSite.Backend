using JZVerse.MicroHuaxia.ConfigCenter.Abstractions;
using JZVerse.MicroHuaxia.ConfigCenter.Core.Persistence;
using JZVerse.MicroHuaxia.ConfigCenter.Server.Configuration;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.ConfigCenter.Server.BackgroundServices;

/// <summary>
/// 配置快照后台服务
/// </summary>
public class ConfigSnapshotBackgroundService(
    IConfigItemRepository _repository,
    FileConfigStore _store,
    IOptions<ConfigCenterServerOptions> _options,
    ILogger<ConfigSnapshotBackgroundService> _logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.EnablePersistence)
        {
            _logger.LogInformation("Config persistence is disabled, snapshot service will not run");
            return;
        }

        _logger.LogInformation(
            "Config snapshot service started, interval: {Interval} minutes",
            _options.Value.SnapshotIntervalMinutes);

        var interval = TimeSpan.FromMinutes(_options.Value.SnapshotIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
                await SaveSnapshotsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving config snapshots");
            }
        }

        _logger.LogInformation("Config snapshot service stopped");
    }

    private async Task SaveSnapshotsAsync(CancellationToken cancellationToken)
    {
        var items = await _repository.GetAllAsync(cancellationToken);

        // 按命名空间和环境分组
        var groups = items.GroupBy(i => (i.NamespaceId, i.EnvironmentId));

        foreach (var group in groups)
        {
            var config = group.ToDictionary(i => i.Key, i => i.Value);
            await _store.SaveSnapshotAsync(
                group.Key.NamespaceId,
                group.Key.EnvironmentId,
                config,
                cancellationToken);
        }

        _logger.LogDebug("Saved snapshots for {Count} namespace-environment combinations", groups.Count());
    }
}
