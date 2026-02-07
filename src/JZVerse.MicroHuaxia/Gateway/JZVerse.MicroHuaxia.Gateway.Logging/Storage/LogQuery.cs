using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Gateway.Logging.Storage;

/// <summary>
/// 日志查询条件
/// </summary>
public sealed class LogQuery
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
    /// 精确匹配的日志级别列表
    /// </summary>
    public List<LogLevel>? Levels { get; set; }

    /// <summary>
    /// 全文搜索关键词
    /// </summary>
    public string? SearchText { get; set; }

    /// <summary>
    /// 是否在消息中搜索
    /// </summary>
    public bool SearchInMessage { get; set; } = true;

    /// <summary>
    /// 是否在异常中搜索
    /// </summary>
    public bool SearchInException { get; set; } = true;

    /// <summary>
    /// 是否区分大小写
    /// </summary>
    public bool CaseSensitive { get; set; }

    /// <summary>
    /// 服务名称过滤
    /// </summary>
    public string? ServiceName { get; set; }

    /// <summary>
    /// 类别名称过滤
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// 事件 ID 过滤
    /// </summary>
    public int? EventId { get; set; }

    /// <summary>
    /// 日志来源过滤
    /// </summary>
    public LogSource? Source { get; set; }

    /// <summary>
    /// 追踪 ID 精确匹配
    /// </summary>
    public string? TraceId { get; set; }

    /// <summary>
    /// 请求 ID 精确匹配
    /// </summary>
    public string? RequestId { get; set; }

    /// <summary>
    /// 请求路径过滤（支持通配符 *）
    /// </summary>
    public string? RequestPath { get; set; }

    /// <summary>
    /// 请求方法过滤
    /// </summary>
    public string? RequestMethod { get; set; }

    /// <summary>
    /// 最小状态码
    /// </summary>
    public int? StatusCodeMin { get; set; }

    /// <summary>
    /// 最大状态码
    /// </summary>
    public int? StatusCodeMax { get; set; }

    /// <summary>
    /// 客户端 IP 过滤
    /// </summary>
    public string? ClientIp { get; set; }

    /// <summary>
    /// 用户 ID 过滤
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// 属性键值对过滤
    /// </summary>
    public Dictionary<string, string>? Properties { get; set; }

    /// <summary>
    /// 标签过滤（匹配任一标签）
    /// </summary>
    public List<string>? Tags { get; set; }

    /// <summary>
    /// 跳过条数
    /// </summary>
    public int Skip { get; set; }

    /// <summary>
    /// 获取条数（最大 1000）
    /// </summary>
    public int Take { get; set; } = 100;

    /// <summary>
    /// 排序字段
    /// </summary>
    public LogSortField SortBy { get; set; } = LogSortField.Timestamp;

    /// <summary>
    /// 是否降序排序
    /// </summary>
    public bool Descending { get; set; } = true;
}

/// <summary>
/// 日志排序字段
/// </summary>
public enum LogSortField
{
    /// <summary>
    /// 按时间戳排序
    /// </summary>
    Timestamp,

    /// <summary>
    /// 按日志级别排序
    /// </summary>
    Level
}
