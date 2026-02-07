using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Telemetry.Logging;

/// <summary>
/// 日志条目
/// </summary>
public sealed record LogEntry
{
    /// <summary>
    /// 日志 ID
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 日志级别
    /// </summary>
    public LogLevel Level { get; init; }

    /// <summary>
    /// 类别名称
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// 事件 ID
    /// </summary>
    public int EventId { get; init; }

    /// <summary>
    /// 日志消息
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 异常信息
    /// </summary>
    public string? Exception { get; init; }

    /// <summary>
    /// Trace ID (分布式追踪关联)
    /// </summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// Span ID
    /// </summary>
    public string? SpanId { get; init; }

    /// <summary>
    /// 服务实例 ID (如果日志来自服务操作)
    /// </summary>
    public string? ServiceInstanceId { get; init; }

    /// <summary>
    /// 服务名称
    /// </summary>
    public string? ServiceName { get; init; }

    /// <summary>
    /// 结构化属性
    /// </summary>
    public IReadOnlyDictionary<string, object?> Properties { get; init; } = new Dictionary<string, object?>();

    /// <summary>
    /// Scope 信息
    /// </summary>
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Scopes { get; init; } = [];
}

/// <summary>
/// 日志查询条件
/// </summary>
public sealed class LogQuery
{
    /// <summary>
    /// 最小日志级别
    /// </summary>
    public LogLevel? MinLevel { get; set; }

    /// <summary>
    /// 类别名称过滤
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// 搜索文本
    /// </summary>
    public string? SearchText { get; set; }

    /// <summary>
    /// 服务名称过滤
    /// </summary>
    public string? ServiceName { get; set; }

    /// <summary>
    /// Trace ID 过滤
    /// </summary>
    public string? TraceId { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTimeOffset? StartTime { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>
    /// 跳过条数
    /// </summary>
    public int? Skip { get; set; }

    /// <summary>
    /// 获取条数
    /// </summary>
    public int? Take { get; set; } = 100;

    /// <summary>
    /// 是否降序 (按时间)
    /// </summary>
    public bool Descending { get; set; } = true;
}

/// <summary>
/// 日志查询结果
/// </summary>
public sealed class LogQueryResult
{
    /// <summary>
    /// 日志列表
    /// </summary>
    public IReadOnlyList<LogEntry> Logs { get; init; } = [];

    /// <summary>
    /// 符合条件的总数
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// 历史总日志数
    /// </summary>
    public long TotalLogsEverReceived { get; init; }
}

/// <summary>
/// 日志统计
/// </summary>
public sealed class LogStatistics
{
    /// <summary>
    /// 当前存储的日志数
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// 历史总接收日志数
    /// </summary>
    public long TotalEverReceived { get; init; }

    /// <summary>
    /// 按级别分布
    /// </summary>
    public IReadOnlyDictionary<string, int> ByLevel { get; init; } = new Dictionary<string, int>();

    /// <summary>
    /// 最后一条日志时间
    /// </summary>
    public DateTimeOffset? LastLogTime { get; init; }
}
