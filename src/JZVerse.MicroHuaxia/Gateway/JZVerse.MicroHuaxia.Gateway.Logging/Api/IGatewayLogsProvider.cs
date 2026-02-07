using JZVerse.MicroHuaxia.Gateway.Logging.Storage;

namespace JZVerse.MicroHuaxia.Gateway.Logging.Api;

/// <summary>
/// 日志查询 API 提供者接口
/// </summary>
public interface IGatewayLogsProvider
{
    /// <summary>
    /// 查询日志
    /// </summary>
    Task<LogQueryResult> QueryAsync(LogQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据 ID 获取日志
    /// </summary>
    Task<LogEntry?> GetByIdAsync(string logId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据 TraceId 获取关联日志
    /// </summary>
    Task<IReadOnlyList<LogEntry>> GetByTraceIdAsync(string traceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取最新日志
    /// </summary>
    Task<IReadOnlyList<LogEntry>> GetLatestAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取统计信息
    /// </summary>
    Task<LogStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取可用的服务名称列表
    /// </summary>
    Task<IReadOnlyList<string>> GetServiceNamesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 清理过期日志
    /// </summary>
    Task CleanupAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 清空所有日志
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
