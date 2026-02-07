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
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Config sync service started");

        // 启动时立即加载配置
        await LoadInitialConfigAsync(stoppingToken);

        // 根据通知模式决定同步策略
        switch (_options.Value.NotificationMode)
        {
            case NotificationMode.Polling:
                await RunPollingLoopAsync(stoppingToken);
                break;

            case NotificationMode.LongPolling:
            case NotificationMode.WebSocket:
                // TODO: 实现长轮询和 WebSocket 模式
                _logger.LogWarning(
                    "Notification mode {Mode} is not yet implemented, falling back to polling",
                    _options.Value.NotificationMode);
                await RunPollingLoopAsync(stoppingToken);
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
}
