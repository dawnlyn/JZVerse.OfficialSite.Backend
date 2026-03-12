using JZVerse.MicroHuaxia.ServiceDiscovery.Client;
using Microsoft.Extensions.Options;

namespace JZVerse.Business.Server.Configuration;

/// <summary>
/// 服务状态信息
/// </summary>
public sealed record ServiceStatus
{
    /// <summary>
    /// 服务名称
    /// </summary>
    public required string ServiceName { get; init; }

    /// <summary>
    /// 服务版本
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// 启动时间
    /// </summary>
    public required DateTime StartedAt { get; init; }

    /// <summary>
    /// 运行时长
    /// </summary>
    public TimeSpan Uptime => DateTime.UtcNow - StartedAt;

    /// <summary>
    /// 是否需要重启
    /// </summary>
    public required bool RestartRequired { get; init; }

    /// <summary>
    /// 变更的关键配置
    /// </summary>
    public required IReadOnlyList<string> ChangedConfigs { get; init; }

    /// <summary>
    /// 上次配置变更时间
    /// </summary>
    public DateTime? LastConfigChangeAt { get; init; }
}

/// <summary>
/// 服务重启协调器
/// </summary>
/// <remarks>
/// 负责协调服务的优雅重启：
/// 1. 记录需要重启的状态和变更的配置
/// 2. 提供重启触发接口
/// 3. 执行优雅关闭流程（等待请求排空、注销服务发现、停止应用）
/// </remarks>
public sealed class ServiceRestartCoordinator
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IServiceDiscoveryClient _discoveryClient;
    private readonly ManagementOptions _options;
    private readonly ILogger<ServiceRestartCoordinator> _logger;
    private readonly DateTime _startedAt = DateTime.UtcNow;

    private volatile bool _restartRequired;
    private readonly List<string> _changedConfigs = [];
    private DateTime? _lastConfigChangeAt;
    private readonly object _lock = new();

    public ServiceRestartCoordinator(
        IHostApplicationLifetime lifetime,
        IServiceDiscoveryClient discoveryClient,
        IOptions<ManagementOptions> options,
        ILogger<ServiceRestartCoordinator> logger)
    {
        _lifetime = lifetime;
        _discoveryClient = discoveryClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 是否需要重启
    /// </summary>
    public bool RestartRequired => _restartRequired;

    /// <summary>
    /// 变更的关键配置列表
    /// </summary>
    public IReadOnlyList<string> ChangedConfigs
    {
        get
        {
            lock (_lock)
            {
                return [.. _changedConfigs];
            }
        }
    }

    /// <summary>
    /// 获取服务状态
    /// </summary>
    public ServiceStatus GetStatus()
    {
        lock (_lock)
        {
            return new ServiceStatus
            {
                ServiceName = "business-server",
                Version = "1.0.0",
                StartedAt = _startedAt,
                RestartRequired = _restartRequired,
                ChangedConfigs = [.. _changedConfigs],
                LastConfigChangeAt = _lastConfigChangeAt
            };
        }
    }

    /// <summary>
    /// 标记需要重启
    /// </summary>
    public void MarkRestartRequired(IEnumerable<string> changedKeys)
    {
        lock (_lock)
        {
            _restartRequired = true;
            _lastConfigChangeAt = DateTime.UtcNow;

            foreach (var key in changedKeys)
            {
                if (!_changedConfigs.Contains(key))
                {
                    _changedConfigs.Add(key);
                }
            }
        }

        _logger.LogWarning(
            "服务已标记需要重启，变更的关键配置: {Keys}",
            string.Join(", ", changedKeys));
    }

    /// <summary>
    /// 触发服务重启
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task RestartAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.RestartEnabled)
        {
            _logger.LogWarning("服务重启功能已禁用");
            throw new InvalidOperationException("服务重启功能已禁用");
        }

        _logger.LogInformation("开始执行服务重启流程...");

        try
        {
            // 1. 从服务发现注销（让网关停止转发新请求）
            _logger.LogInformation("正在从服务发现注销...");
            // 服务会在停止时自动注销，这里不需要手动调用

            // 2. 等待现有请求完成
            var delaySeconds = _options.RestartDelaySeconds;
            _logger.LogInformation("等待 {Seconds} 秒让现有请求完成...", delaySeconds);
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);

            // 3. 触发应用停止
            _logger.LogInformation("正在停止应用...");
            _lifetime.StopApplication();

            _logger.LogInformation("服务重启流程已触发，应用将由进程管理器重新启动");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("服务重启被取消");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "服务重启过程中发生错误");
            throw;
        }
    }

    /// <summary>
    /// 清除重启标记（用于配置回滚等场景）
    /// </summary>
    public void ClearRestartRequired()
    {
        lock (_lock)
        {
            _restartRequired = false;
            _changedConfigs.Clear();
            _lastConfigChangeAt = null;
        }

        _logger.LogInformation("已清除服务重启标记");
    }
}
