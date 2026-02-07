namespace JZVerse.MicroHuaxia.Gateway.Logging.Storage;

/// <summary>
/// 日志查询结果
/// </summary>
public sealed class LogQueryResult
{
    /// <summary>
    /// 日志条目列表
    /// </summary>
    public IReadOnlyList<LogEntry> Entries { get; init; } = [];

    /// <summary>
    /// 总条数（不含分页）
    /// </summary>
    public long TotalCount { get; init; }

    /// <summary>
    /// 是否有更多数据
    /// </summary>
    public bool HasMore { get; init; }

    /// <summary>
    /// 查询耗时（毫秒）
    /// </summary>
    public double QueryDurationMs { get; init; }

    /// <summary>
    /// 查询开始时间
    /// </summary>
    public DateTimeOffset? StartTime { get; init; }

    /// <summary>
    /// 查询结束时间
    /// </summary>
    public DateTimeOffset? EndTime { get; init; }
}
