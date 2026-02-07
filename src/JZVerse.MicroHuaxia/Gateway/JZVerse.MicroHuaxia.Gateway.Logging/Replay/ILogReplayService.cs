using System.Runtime.CompilerServices;
using JZVerse.MicroHuaxia.Gateway.Logging.Storage;

namespace JZVerse.MicroHuaxia.Gateway.Logging.Replay;

/// <summary>
/// 日志回放服务接口
/// </summary>
public interface ILogReplayService
{
    /// <summary>
    /// 基本回放（按条件查询）
    /// </summary>
    Task<LogQueryResult> ReplayAsync(LogQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按 TraceId 回放（获取完整请求链路）
    /// </summary>
    Task<TraceReplayResult> ReplayByTraceAsync(string traceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按 RequestId 回放
    /// </summary>
    Task<IReadOnlyList<LogEntry>> ReplayByRequestAsync(string requestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 慢查询分析
    /// </summary>
    Task<IReadOnlyList<SlowQueryInfo>> AnalyzeSlowQueriesAsync(
        int thresholdMs,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 实时流式回放
    /// </summary>
    IAsyncEnumerable<LogEntry> StreamLogsAsync(
        LogQuery? filter = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// TraceId 回放结果
/// </summary>
public sealed class TraceReplayResult
{
    /// <summary>
    /// TraceId
    /// </summary>
    public string TraceId { get; init; } = string.Empty;

    /// <summary>
    /// 日志条目（按时间排序）
    /// </summary>
    public IReadOnlyList<LogEntry> Entries { get; init; } = [];

    /// <summary>
    /// 请求开始时间
    /// </summary>
    public DateTimeOffset? StartTime { get; init; }

    /// <summary>
    /// 请求结束时间
    /// </summary>
    public DateTimeOffset? EndTime { get; init; }

    /// <summary>
    /// 总耗时（毫秒）
    /// </summary>
    public double? TotalDurationMs { get; init; }

    /// <summary>
    /// 涉及的服务列表
    /// </summary>
    public IReadOnlyList<string> Services { get; init; } = [];

    /// <summary>
    /// 是否包含错误
    /// </summary>
    public bool HasErrors { get; init; }

    /// <summary>
    /// 请求阶段
    /// </summary>
    public IReadOnlyList<RequestPhase> Phases { get; init; } = [];
}

/// <summary>
/// 请求阶段
/// </summary>
public sealed class RequestPhase
{
    /// <summary>
    /// 阶段名称
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTimeOffset StartTime { get; init; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTimeOffset? EndTime { get; init; }

    /// <summary>
    /// 耗时（毫秒）
    /// </summary>
    public double? DurationMs { get; init; }

    /// <summary>
    /// 服务名称
    /// </summary>
    public string? ServiceName { get; init; }

    /// <summary>
    /// 日志条目
    /// </summary>
    public IReadOnlyList<LogEntry> Entries { get; init; } = [];
}

/// <summary>
/// 慢查询信息
/// </summary>
public sealed class SlowQueryInfo
{
    /// <summary>
    /// 请求路径
    /// </summary>
    public string RequestPath { get; init; } = string.Empty;

    /// <summary>
    /// 请求方法
    /// </summary>
    public string? RequestMethod { get; init; }

    /// <summary>
    /// 请求次数
    /// </summary>
    public int Count { get; init; }

    /// <summary>
    /// 平均耗时（毫秒）
    /// </summary>
    public double AvgDurationMs { get; init; }

    /// <summary>
    /// 最大耗时（毫秒）
    /// </summary>
    public double MaxDurationMs { get; init; }

    /// <summary>
    /// 最小耗时（毫秒）
    /// </summary>
    public double MinDurationMs { get; init; }

    /// <summary>
    /// P95 耗时（毫秒）
    /// </summary>
    public double P95DurationMs { get; init; }

    /// <summary>
    /// P99 耗时（毫秒）
    /// </summary>
    public double P99DurationMs { get; init; }

    /// <summary>
    /// 示例 TraceId 列表
    /// </summary>
    public IReadOnlyList<string> SampleTraceIds { get; init; } = [];
}
