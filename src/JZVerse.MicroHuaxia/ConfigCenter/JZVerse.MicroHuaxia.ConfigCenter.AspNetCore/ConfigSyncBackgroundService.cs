using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.ConfigCenter.Client;
using JZVerse.MicroHuaxia.ConfigCenter.Client.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.ConfigCenter.AspNetCore;

/// <summary>
/// 配置同步后台服务
/// </summary>
public class ConfigSyncBackgroundService(
    IConfigCenterClient _client,
    IOptions<ConfigCenterClientOptions> _options,
    ILogger<ConfigSyncBackgroundService> _logger
) : BackgroundService
{
    /// <summary>
    /// 每个 namespace:env 的已知版本号（用于 LongPolling）
    /// </summary>
    private readonly ConcurrentDictionary<string, long> _knownVersions = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Config sync service started, mode={Mode}", _options.Value.NotificationMode);

        // 启动时立即加载配置
        await LoadInitialConfigAsync(stoppingToken);

        // 根据通知模式决定同步策略
        switch (_options.Value.NotificationMode)
        {
            case NotificationMode.LongPolling:
                await RunLongPollingLoopAsync(stoppingToken);
                break;

            case NotificationMode.Polling:
                await RunPollingLoopAsync(stoppingToken);
                break;

            case NotificationMode.WebSocket:
                // WebSocket 模式暂未实现，降级为长轮询
                _logger.LogWarning("WebSocket mode is not yet implemented, falling back to LongPolling");
                await RunLongPollingLoopAsync(stoppingToken);
                break;
        }

        _logger.LogInformation("Config sync service stopped");
    }

    private async Task LoadInitialConfigAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _client.RefreshAsync(cancellationToken);
            _logger.LogInformation("Initial config loaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load initial config");
        }
    }

    private async Task RunPollingLoopAsync(CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromSeconds(_options.Value.PollingIntervalSeconds);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, cancellationToken);
                await _client.RefreshAsync(cancellationToken);
                _logger.LogDebug("Config refreshed via polling");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during config polling");
            }
        }
    }

    private async Task RunLongPollingLoopAsync(CancellationToken cancellationToken)
    {
        var namespaces = _options.Value.Namespaces;
        var environmentId = _options.Value.EnvironmentId;
        const int watchTimeoutSeconds = 30;

        if (namespaces.Count == 0)
        {
            _logger.LogWarning("No namespaces configured, LongPolling will not run");
            return;
        }

        _logger.LogInformation(
            "Starting LongPolling loop for {Count} namespace(s): {Namespaces}",
            namespaces.Count, string.Join(", ", namespaces));

        // 为每个命名空间并行运行长轮询
        var tasks = namespaces.Select(ns =>
            WatchNamespaceLoopAsync(ns, environmentId, watchTimeoutSeconds, cancellationToken));

        await Task.WhenAll(tasks);
    }

    private async Task WatchNamespaceLoopAsync(
        string namespaceId,
        string environmentId,
        int watchTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        var key = $"{namespaceId}:{environmentId}";
        var consecutiveFailures = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var lastVersion = _knownVersions.GetValueOrDefault(key, 0);

                var result = await _client.WatchAsync(
                    namespaceId, environmentId, lastVersion, watchTimeoutSeconds, cancellationToken);

                if (result is { HasChanged: true })
                {
                    _logger.LogInformation(
                        "Config change detected for {Namespace}:{Environment}, version {OldVer} -> {NewVer}",
                        namespaceId, environmentId, lastVersion, result.Version);

                    // 更新本地版本号
                    _knownVersions[key] = result.Version;

                    // 如果 watch 返回了配置，直接使用；否则刷新
                    if (result.Config is null)
                    {
                        await _client.RefreshAsync(cancellationToken);
                    }

                    consecutiveFailures = 0;
                }
                else if (result is not null)
                {
                    // 无变更（超时），更新版本号后立即重新 watch
                    _knownVersions[key] = result.Version;
                    consecutiveFailures = 0;
                }
                else
                {
                    // result == null → watch 请求失败，短暂等待后重试
                    consecutiveFailures++;
                    await BackoffDelayAsync(consecutiveFailures, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                consecutiveFailures++;
                _logger.LogError(ex,
                    "LongPolling error for {Namespace}:{Environment}, failure #{Count}",
                    namespaceId, environmentId, consecutiveFailures);
                await BackoffDelayAsync(consecutiveFailures, cancellationToken);
            }
        }
    }

    /// <summary>
    /// 指数退避延迟（最大 60 秒）
    /// </summary>
    private static async Task BackoffDelayAsync(int failureCount, CancellationToken cancellationToken)
    {
        var delaySeconds = Math.Min(60, Math.Pow(2, Math.Min(failureCount, 6)));
        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
    }
}
