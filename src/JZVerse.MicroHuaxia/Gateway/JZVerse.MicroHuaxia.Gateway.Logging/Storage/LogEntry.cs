using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Gateway.Logging.Storage;

/// <summary>
/// 日志条目模型
/// </summary>
public sealed class LogEntry
{
    /// <summary>
    /// 日志唯一标识符
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
    /// 类别名称（通常是记录器的完全限定名）
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// 事件 ID
    /// </summary>
    public int EventId { get; init; }

    /// <summary>
    /// 事件名称
    /// </summary>
    public string? EventName { get; init; }

    /// <summary>
    /// 日志消息
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 异常信息
    /// </summary>
    public string? Exception { get; init; }

    /// <summary>
    /// 追踪 ID（与分布式追踪关联）
    /// </summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// Span ID
    /// </summary>
    public string? SpanId { get; init; }

    /// <summary>
    /// 父 Span ID
    /// </summary>
    public string? ParentSpanId { get; init; }

    /// <summary>
    /// 服务名称
    /// </summary>
    public string? ServiceName { get; init; }

    /// <summary>
    /// 服务实例 ID
    /// </summary>
    public string? ServiceInstanceId { get; init; }

    /// <summary>
    /// 主机名
    /// </summary>
    public string? HostName { get; init; }

    /// <summary>
    /// 环境名称（Development/Production）
    /// </summary>
    public string? Environment { get; init; }

    /// <summary>
    /// 请求路径
    /// </summary>
    public string? RequestPath { get; init; }

    /// <summary>
    /// 请求方法
    /// </summary>
    public string? RequestMethod { get; init; }

    /// <summary>
    /// 请求 ID
    /// </summary>
    public string? RequestId { get; init; }

    /// <summary>
    /// 客户端 IP
    /// </summary>
    public string? ClientIp { get; init; }

    /// <summary>
    /// 用户 ID
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// 响应状态码
    /// </summary>
    public int? StatusCode { get; init; }

    /// <summary>
    /// 请求持续时间（毫秒）
    /// </summary>
    public double? DurationMs { get; init; }

    /// <summary>
    /// 结构化属性
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Properties { get; init; }

    /// <summary>
    /// Scope 信息
    /// </summary>
    public IReadOnlyList<IReadOnlyDictionary<string, object?>>? Scopes { get; init; }

    /// <summary>
    /// 日志来源
    /// </summary>
    public LogSource Source { get; init; } = LogSource.Application;

    /// <summary>
    /// 自定义标签
    /// </summary>
    public IReadOnlyList<string>? Tags { get; init; }
}

/// <summary>
/// 日志来源
/// </summary>
public enum LogSource
{
    /// <summary>
    /// 应用程序日志
    /// </summary>
    Application,

    /// <summary>
    /// 中间件日志
    /// </summary>
    Middleware,

    /// <summary>
    /// 系统日志
    /// </summary>
    System
}
