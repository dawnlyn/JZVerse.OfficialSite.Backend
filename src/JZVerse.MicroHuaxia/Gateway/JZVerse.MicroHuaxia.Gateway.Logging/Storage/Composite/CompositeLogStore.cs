using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Gateway.Logging.Storage.Composite;

/// <summary>
/// 组合日志存储（同时写入多个存储）
/// </summary>
public sealed class CompositeLogStore : ILogStore
{
    private readonly IReadOnlyList<ILogStore> _stores;
    private readonly ILogStore _primaryStore;
    private readonly ILogger<CompositeLogStore> _logger;

    /// <summary>
    /// 创建组合存储
    /// </summary>
    /// <param name="stores">所有存储（第一个为主存储）</param>
    /// <param name="logger">日志记录器</param>
    public CompositeLogStore(
        IEnumerable<ILogStore> stores,
        ILogger<CompositeLogStore> logger)
    {
        _stores = stores.ToList();
        _primaryStore = _stores.FirstOrDefault()
            ?? throw new ArgumentException("至少需要一个存储", nameof(stores));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task AddAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        var tasks = _stores.Select(store => SafeAddAsync(store, entry, cancellationToken));
        await Task.WhenAll(tasks);
    }

    /// <inheritdoc />
    public async Task AddBatchAsync(IEnumerable<LogEntry> entries, CancellationToken cancellationToken = default)
    {
        var entriesList = entries.ToList();
        var tasks = _stores.Select(store => SafeAddBatchAsync(store, entriesList, cancellationToken));
        await Task.WhenAll(tasks);
    }

    /// <inheritdoc />
    public Task<LogQueryResult> QueryAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        // 查询使用主存储
        return _primaryStore.QueryAsync(query, cancellationToken);
    }

    /// <inheritdoc />
    public Task<LogEntry?> GetByIdAsync(string logId, CancellationToken cancellationToken = default)
    {
        return _primaryStore.GetByIdAsync(logId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<LogEntry>> GetByTraceIdAsync(string traceId, CancellationToken cancellationToken = default)
    {
        return _primaryStore.GetByTraceIdAsync(traceId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<LogEntry>> GetLatestAsync(int count, CancellationToken cancellationToken = default)
    {
        return _primaryStore.GetLatestAsync(count, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<LogStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        // 合并所有存储的统计信息
        var stats = await _primaryStore.GetStatisticsAsync(cancellationToken);
        return stats;
    }

    /// <inheritdoc />
    public async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        var tasks = _stores.Select(store => SafeCleanupAsync(store, cancellationToken));
        await Task.WhenAll(tasks);
    }

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        var tasks = _stores.Select(store => SafeClearAsync(store, cancellationToken));
        await Task.WhenAll(tasks);
    }

    /// <inheritdoc />
    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        var tasks = _stores.Select(store => store.HealthCheckAsync(cancellationToken));
        var results = await Task.WhenAll(tasks);
        return results.All(r => r);
    }

    private async Task SafeAddAsync(ILogStore store, LogEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            await store.AddAsync(entry, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "写入存储失败: {StoreType}", store.GetType().Name);
        }
    }

    private async Task SafeAddBatchAsync(ILogStore store, IEnumerable<LogEntry> entries, CancellationToken cancellationToken)
    {
        try
        {
            await store.AddBatchAsync(entries, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "批量写入存储失败: {StoreType}", store.GetType().Name);
        }
    }

    private async Task SafeCleanupAsync(ILogStore store, CancellationToken cancellationToken)
    {
        try
        {
            await store.CleanupAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "清理存储失败: {StoreType}", store.GetType().Name);
        }
    }

    private async Task SafeClearAsync(ILogStore store, CancellationToken cancellationToken)
    {
        try
        {
            await store.ClearAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "清空存储失败: {StoreType}", store.GetType().Name);
        }
    }
}
