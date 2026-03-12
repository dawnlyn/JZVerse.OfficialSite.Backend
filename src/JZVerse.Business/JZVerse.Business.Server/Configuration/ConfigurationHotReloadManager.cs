using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;
using JZVerse.MicroHuaxia.ConfigCenter.Client;
using Microsoft.Extensions.Options;

namespace JZVerse.Business.Server.Configuration;

/// <summary>
/// 管理配置选项
/// </summary>
public sealed class ManagementOptions
{
    public const string SectionName = "Management";

    /// <summary>
    /// 是否启用重启功能
    /// </summary>
    public bool RestartEnabled { get; set; } = true;

    /// <summary>
    /// 重启延迟秒数（等待请求排空）
    /// </summary>
    public int RestartDelaySeconds { get; set; } = 5;

    /// <summary>
    /// 关键配置键列表（修改后需要重启）
    /// </summary>
    public List<string> CriticalConfigKeys { get; set; } =
    [
        "Authentication:Jwt:SecretKey",
        "Encryption:Sm2:PrivateKey",
        "Encryption:Sm2:PublicKey",
        "Kestrel:Endpoints"
    ];
}

/// <summary>
/// 配置热重载管理器
/// </summary>
/// <remarks>
/// 负责监听 ConfigCenter 的配置变更事件，区分普通配置和关键配置。
/// - 普通配置：通过 IOptionsMonitor 自动生效
/// - 关键配置：标记需要重启，等待手动触发
/// </remarks>
public sealed class ConfigurationHotReloadManager(
    IConfigCenterClient _configClient,
    ServiceRestartCoordinator _restartCoordinator,
    IOptions<ManagementOptions> _managementOptions,
    IConfiguration _configuration,
    ILogger<ConfigurationHotReloadManager> _logger
) : BackgroundService
{
    private readonly ManagementOptions _options = _managementOptions.Value;
    private readonly List<string> _subscribedNamespaces = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("配置热重载管理器已启动");

        // 订阅所有配置命名空间
        var namespaces = _configuration.GetSection("ConfigCenter:Client:Namespaces").Get<List<string>>() ?? [];

        foreach (var ns in namespaces)
        {
            try
            {
                await _configClient.SubscribeAsync(ns, OnConfigChanged, stoppingToken);
                _subscribedNamespaces.Add(ns);
                _logger.LogInformation("已订阅配置命名空间: {Namespace}", ns);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "订阅配置命名空间失败: {Namespace}", ns);
            }
        }

        // 保持运行直到取消
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // 正常退出
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("配置热重载管理器正在停止...");

        // 取消所有订阅
        foreach (var ns in _subscribedNamespaces)
        {
            try
            {
                await _configClient.UnsubscribeAsync(ns, cancellationToken);
                _logger.LogInformation("已取消订阅配置命名空间: {Namespace}", ns);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取消订阅配置命名空间失败: {Namespace}", ns);
            }
        }

        await base.StopAsync(cancellationToken);
    }

    private Task OnConfigChanged(ConfigChangeEvent changeEvent)
    {
        _logger.LogInformation(
            "收到配置变更事件: Namespace={Namespace}, EventType={EventType}",
            changeEvent.NamespaceId,
            changeEvent.EventType);

        // 检查是否有关键配置变更
        var changedKeys = changeEvent.ChangedItems?.Select(x => x.Key).ToList() ?? [];
        var criticalChanges = changedKeys
            .Where(key => _options.CriticalConfigKeys.Any(critical =>
                key.StartsWith(critical, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (criticalChanges.Count > 0)
        {
            _logger.LogWarning(
                "检测到关键配置变更，需要重启服务: {Keys}",
                string.Join(", ", criticalChanges));

            _restartCoordinator.MarkRestartRequired(criticalChanges);
        }
        else
        {
            _logger.LogInformation("普通配置变更，将自动生效: {Keys}", string.Join(", ", changedKeys));
        }

        return Task.CompletedTask;
    }
}
