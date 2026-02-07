namespace JZVerse.MicroHuaxia.Gateway.Logging.Storage;

/// <summary>
/// 日志存储接口
/// </summary>
public interface ILogStore
{
    /// <summary>
    /// 添加单条日志
    /// </summary>
    Task AddAsync(LogEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量添加日志
    /// </summary>
    Task AddBatchAsync(IEnumerable<LogEntry> entries, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询日志
    /// </summary>
    Task<LogQueryResult> QueryAsync(LogQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据 ID 获取单条日志
    /// </summary>
    Task<LogEntry?> GetByIdAsync(string logId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据 TraceId 获取关联日志
    /// </summary>
    Task<IReadOnlyList<LogEntry>> GetByTraceIdAsync(string traceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取最新的日志
    /// </summary>
    Task<IReadOnlyList<LogEntry>> GetLatestAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取统计信息
    /// </summary>
    Task<LogStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 清理过期数据
    /// </summary>
    Task CleanupAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 清空所有数据
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 健康检查
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 支持实时流的日志存储接口
/// </summary>
public interface IStreamableLogStore : ILogStore
{
    /// <summary>
    /// 当新日志添加时触发
    /// </summary>
    event EventHandler<LogEntry>? LogAdded;

    /// <summary>
    /// 订阅日志流
    /// </summary>
    IAsyncEnumerable<LogEntry> StreamAsync(LogQuery? filter = null, CancellationToken cancellationToken = default);
}
