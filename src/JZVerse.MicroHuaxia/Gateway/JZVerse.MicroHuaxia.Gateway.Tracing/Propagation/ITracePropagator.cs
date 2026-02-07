using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace JZVerse.MicroHuaxia.Gateway.Tracing.Propagation;

/// <summary>
/// 追踪传播上下文
/// </summary>
public readonly struct PropagationContext
{
    /// <summary>
    /// 追踪 ID
    /// </summary>
    public ActivityTraceId TraceId { get; }

    /// <summary>
    /// Span ID
    /// </summary>
    public ActivitySpanId SpanId { get; }

    /// <summary>
    /// 追踪标志
    /// </summary>
    public ActivityTraceFlags TraceFlags { get; }

    /// <summary>
    /// 追踪状态（W3C tracestate）
    /// </summary>
    public string? TraceState { get; }

    /// <summary>
    /// 是否有效
    /// </summary>
    public bool IsValid => TraceId != default && SpanId != default;

    public PropagationContext(
        ActivityTraceId traceId,
        ActivitySpanId spanId,
        ActivityTraceFlags traceFlags = ActivityTraceFlags.None,
        string? traceState = null
    )
    {
        TraceId = traceId;
        SpanId = spanId;
        TraceFlags = traceFlags;
        TraceState = traceState;
    }

    public static PropagationContext Empty => default;
}

/// <summary>
/// 追踪传播器接口
/// 用于在服务间传播追踪上下文
/// </summary>
public interface ITracePropagator
{
    /// <summary>
    /// 传播器名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 将追踪上下文注入到请求消息中
    /// </summary>
    /// <param name="activity">当前 Activity</param>
    /// <param name="request">HTTP 请求消息</param>
    void Inject(Activity? activity, HttpRequestMessage request);

    /// <summary>
    /// 从 HTTP 上下文中提取追踪上下文
    /// </summary>
    /// <param name="context">HTTP 上下文</param>
    /// <returns>提取的传播上下文</returns>
    PropagationContext Extract(HttpContext context);

    /// <summary>
    /// 从请求头中提取追踪上下文
    /// </summary>
    /// <param name="headers">请求头集合</param>
    /// <returns>提取的传播上下文</returns>
    PropagationContext Extract(IHeaderDictionary headers);
}
