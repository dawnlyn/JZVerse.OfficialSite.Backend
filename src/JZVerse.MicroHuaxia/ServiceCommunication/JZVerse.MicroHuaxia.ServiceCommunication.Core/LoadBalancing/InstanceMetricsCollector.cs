using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.LoadBalancing;

namespace JZVerse.MicroHuaxia.ServiceCommunication.Core.LoadBalancing;

/// <summary>
/// 服务实例指标收集器实现
/// </summary>
public sealed class InstanceMetricsCollector : IInstanceMetricsCollector, IDisposable
{
    private readonly ConcurrentDictionary<string, InstanceMetrics> _metrics = new();
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    public InstanceMetricsCollector()
    {
        // 每 5 分钟清理过期指标（超过 30 分钟未更新的）
        _cleanupTimer = new Timer(Cleanup, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public void RecordRequest(string instanceId, bool success, TimeSpan duration)
    {
        var metrics = _metrics.GetOrAdd(instanceId, id => new InstanceMetrics(id));
        metrics.RecordRequest(success, duration);
    }

    public void IncrementActiveConnections(string instanceId)
    {
        var metrics = _metrics.GetOrAdd(instanceId, id => new InstanceMetrics(id));
        metrics.IncrementConnections();
    }

    public void DecrementActiveConnections(string instanceId)
    {
        if (_metrics.TryGetValue(instanceId, out var metrics))
        {
            metrics.DecrementConnections();
        }
    }

    public InstanceMetrics? GetMetrics(string instanceId)
    {
        return _metrics.TryGetValue(instanceId, out var metrics) ? metrics : null;
    }

    public IReadOnlyDictionary<string, InstanceMetrics> GetAllMetrics()
    {
        return _metrics;
    }

    public void ClearMetrics(string instanceId)
    {
        _metrics.TryRemove(instanceId, out _);
    }

    private void Cleanup(object? state)
    {
        if (_disposed) return;

        var now = DateTimeOffset.UtcNow;
        var expiredThreshold = TimeSpan.FromMinutes(30);

        var keysToRemove = _metrics
            .Where(kvp => now - kvp.Value.LastUpdateTime > expiredThreshold)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _metrics.TryRemove(key, out _);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cleanupTimer.Dispose();
    }
}
