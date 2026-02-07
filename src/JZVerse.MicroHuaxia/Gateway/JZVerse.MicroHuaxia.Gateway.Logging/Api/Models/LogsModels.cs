using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Gateway.Logging.Api.Models;

/// <summary>
/// 日志查询请求
/// </summary>
public sealed class LogsQueryRequest
{
    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTimeOffset? StartTime { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>
    /// 最小日志级别
    /// </summary>
    public LogLevel? MinLevel { get; set; }

    /// <summary>
    /// 搜索文本
    /// </summary>
    public string? SearchText { get; set; }

    /// <summary>
    /// 服务名称
    /// </summary>
    public string? ServiceName { get; set; }

    /// <summary>
    /// TraceId
    /// </summary>
    public string? TraceId { get; set; }

    /// <summary>
    /// 请求路径
    /// </summary>
    public string? RequestPath { get; set; }

    /// <summary>
    /// 跳过条数
    /// </summary>
    public int Skip { get; set; }

    /// <summary>
    /// 获取条数
    /// </summary>
    public int Take { get; set; } = 100;

    /// <summary>
    /// 是否降序
    /// </summary>
    public bool Descending { get; set; } = true;
}

/// <summary>
/// 日志查询响应
/// </summary>
public sealed class LogsQueryResponse
{
    /// <summary>
    /// 日志条目
    /// </summary>
    public IReadOnlyList<LogEntryDto> Entries { get; init; } = [];

    /// <summary>
    /// 总条数
    /// </summary>
    public long TotalCount { get; init; }

    /// <summary>
    /// 是否有更多
    /// </summary>
    public bool HasMore { get; init; }

    /// <summary>
    /// 查询耗时（毫秒）
    /// </summary>
    public double QueryDurationMs { get; init; }
}

/// <summary>
/// 日志条目 DTO
/// </summary>
public sealed class LogEntryDto
{
    /// <summary>
    /// ID
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// 日志级别
    /// </summary>
    public string Level { get; init; } = string.Empty;

    /// <summary>
    /// 类别
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// 消息
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 异常
    /// </summary>
    public string? Exception { get; init; }

    /// <summary>
    /// TraceId
    /// </summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// 服务名称
    /// </summary>
    public string? ServiceName { get; init; }

    /// <summary>
    /// 请求路径
    /// </summary>
    public string? RequestPath { get; init; }

    /// <summary>
    /// 请求方法
    /// </summary>
    public string? RequestMethod { get; init; }

    /// <summary>
    /// 状态码
    /// </summary>
    public int? StatusCode { get; init; }

    /// <summary>
    /// 耗时（毫秒）
    /// </summary>
    public double? DurationMs { get; init; }
}

/// <summary>
/// 慢查询分析请求
/// </summary>
public sealed class SlowQueryAnalysisRequest
{
    /// <summary>
    /// 阈值（毫秒）
    /// </summary>
    public int ThresholdMs { get; set; } = 1000;

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTimeOffset EndTime { get; set; }
}

/// <summary>
/// 日志统计响应
/// </summary>
public sealed class LogStatisticsResponse
{
    /// <summary>
    /// 总条数
    /// </summary>
    public long TotalCount { get; init; }

    /// <summary>
    /// 按级别统计
    /// </summary>
    public Dictionary<string, long> CountByLevel { get; init; } = [];

    /// <summary>
    /// 按服务统计
    /// </summary>
    public Dictionary<string, long> CountByService { get; init; } = [];

    /// <summary>
    /// 错误率
    /// </summary>
    public double ErrorRate { get; init; }

    /// <summary>
    /// 每分钟日志数
    /// </summary>
    public double LogsPerMinute { get; init; }

    /// <summary>
    /// 存储使用情况
    /// </summary>
    public StorageStatisticsDto? Storage { get; init; }
}

/// <summary>
/// 存储统计 DTO
/// </summary>
public sealed class StorageStatisticsDto
{
    /// <summary>
    /// 存储类型
    /// </summary>
    public string StorageType { get; init; } = string.Empty;

    /// <summary>
    /// 已使用空间（字节）
    /// </summary>
    public long UsedBytes { get; init; }

    /// <summary>
    /// 最大空间（字节）
    /// </summary>
    public long MaxBytes { get; init; }

    /// <summary>
    /// 使用率
    /// </summary>
    public double UsageRate { get; init; }
}
