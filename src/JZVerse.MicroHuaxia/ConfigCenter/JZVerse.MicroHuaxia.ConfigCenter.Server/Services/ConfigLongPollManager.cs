using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.ConfigCenter.Abstractions;
using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ConfigCenter.Server.Services;

/// <summary>
/// 长轮询管理器，监听配置变更事件并唤醒等待中的客户端
/// </summary>
public sealed class ConfigLongPollManager : IConfigEventListener, IDisposable
{
    private readonly ConcurrentDictionary<string, long> _namespaceVersions = new();
    private readonly ConcurrentDictionary<string, List<TaskCompletionSource<bool>>> _waiters = new();
    private readonly Lock _lock = new();
    private readonly ILogger<ConfigLongPollManager> _logger;

    public ConfigLongPollManager(
        IConfigEventPublisher eventPublisher,
        ILogger<ConfigLongPollManager> logger)
    {
        _logger = logger;
        eventPublisher.Subscribe(this);
    }

    /// <summary>
    /// 获取命名空间当前版本号
    /// </summary>
    public long GetVersion(string namespaceId, string environmentId)
    {
        var key = BuildKey(namespaceId, environmentId);
        return _namespaceVersions.GetValueOrDefault(key, 0);
    }

    /// <summary>
    /// 等待指定命名空间的配置变更
    /// </summary>
    /// <returns>true 表示有变更，false 表示超时</returns>
    public async Task<bool> WaitForChangeAsync(
        string namespaceId,
        string environmentId,
        long clientVersion,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var key = BuildKey(namespaceId, environmentId);

        // 如果服务器版本已经比客户端新，立即返回
        var currentVersion = _namespaceVersions.GetValueOrDefault(key, 0);
        if (currentVersion > clientVersion)
        {
            return true;
        }

        // 注册等待
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_lock)
        {
            if (!_waiters.TryGetValue(key, out var list))
            {
                list = [];
                _waiters[key] = list;
            }
            list.Add(tcs);
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                return await tcs.Task.WaitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                // 超时 → 无变更
                return false;
            }
        }
        finally
        {
            // 清理等待器
            lock (_lock)
            {
                if (_waiters.TryGetValue(key, out var list))
                {
                    list.Remove(tcs);
                    if (list.Count == 0)
                    {
                        _waiters.TryRemove(key, out _);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 处理配置变更事件（由 IConfigEventPublisher 调用）
    /// </summary>
    public Task OnEventAsync(ConfigChangeEvent @event, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(@event.NamespaceId, @event.EnvironmentId);

        // 递增版本号
        _namespaceVersions.AddOrUpdate(key, 1, (_, v) => v + 1);
        var newVersion = _namespaceVersions[key];

        _logger.LogInformation(
            "Config changed for {Key}, new version: {Version}. Waking up watchers.",
            key, newVersion);

        // 唤醒所有等待此 key 的客户端
        List<TaskCompletionSource<bool>>? waiters;
        lock (_lock)
        {
            if (_waiters.TryRemove(key, out waiters))
            {
                // 已从字典中移除
            }
        }

        if (waiters is { Count: > 0 })
        {
            _logger.LogDebug("Waking up {Count} watchers for {Key}", waiters.Count, key);
            foreach (var tcs in waiters)
            {
                tcs.TrySetResult(true);
            }
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // 唤醒所有等待者（取消）
        lock (_lock)
        {
            foreach (var (_, waiters) in _waiters)
            {
                foreach (var tcs in waiters)
                {
                    tcs.TrySetCanceled();
                }
            }
            _waiters.Clear();
        }
    }

    private static string BuildKey(string namespaceId, string environmentId)
        => $"{namespaceId}:{environmentId}";
}
