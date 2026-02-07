using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace JZVerse.MicroHuaxia.Gateway.Tracing.Propagation;

/// <summary>
/// W3C TraceContext 传播器
/// 实现 W3C Trace Context 规范
/// https://www.w3.org/TR/trace-context/
/// </summary>
public sealed class W3CTracePropagator : ITracePropagator
{
    /// <summary>
    /// traceparent 头名称
    /// </summary>
    public const string TraceParentHeader = "traceparent";

    /// <summary>
    /// tracestate 头名称
    /// </summary>
    public const string TraceStateHeader = "tracestate";

    /// <summary>
    /// W3C Trace Context 版本
    /// </summary>
    private const string Version = "00";

    public string Name => "W3C";

    public void Inject(Activity? activity, HttpRequestMessage request)
    {
        if (activity == null)
            return;

        // 格式: 00-{trace-id}-{span-id}-{trace-flags}
        var traceParent =
            $"{Version}-{activity.TraceId.ToHexString()}-{activity.SpanId.ToHexString()}-{((int)activity.ActivityTraceFlags):x2}";

        request.Headers.Remove(TraceParentHeader);
        request.Headers.TryAddWithoutValidation(TraceParentHeader, traceParent);

        // 传播 tracestate
        if (!string.IsNullOrEmpty(activity.TraceStateString))
        {
            request.Headers.Remove(TraceStateHeader);
            request.Headers.TryAddWithoutValidation(TraceStateHeader, activity.TraceStateString);
        }
    }

    public PropagationContext Extract(HttpContext context) => Extract(context.Request.Headers);

    public PropagationContext Extract(IHeaderDictionary headers)
    {
        if (!headers.TryGetValue(TraceParentHeader, out var traceParentValues))
        {
            return PropagationContext.Empty;
        }

        var traceParent = traceParentValues.FirstOrDefault();
        if (string.IsNullOrEmpty(traceParent))
        {
            return PropagationContext.Empty;
        }

        // 解析 traceparent: 00-{trace-id}-{span-id}-{trace-flags}
        var parts = traceParent.Split('-');
        if (parts.Length < 4)
        {
            return PropagationContext.Empty;
        }

        var version = parts[0];
        var traceIdHex = parts[1];
        var spanIdHex = parts[2];
        var flagsHex = parts[3];

        // 验证版本
        if (version != Version)
        {
            // 对于未知版本，仍尝试解析但忽略额外字段
        }

        // 验证 trace-id (32 字符十六进制)
        if (traceIdHex.Length != 32 || traceIdHex == "00000000000000000000000000000000")
        {
            return PropagationContext.Empty;
        }

        // 验证 span-id (16 字符十六进制)
        if (spanIdHex.Length != 16 || spanIdHex == "0000000000000000")
        {
            return PropagationContext.Empty;
        }

        // 解析 trace flags
        if (!byte.TryParse(flagsHex, System.Globalization.NumberStyles.HexNumber, null, out var flags))
        {
            flags = 0;
        }

        // 获取 tracestate
        string? traceState = null;
        if (headers.TryGetValue(TraceStateHeader, out var traceStateValues))
        {
            traceState = traceStateValues.FirstOrDefault();
        }

        return new PropagationContext(
            ActivityTraceId.CreateFromString(traceIdHex.AsSpan()),
            ActivitySpanId.CreateFromString(spanIdHex.AsSpan()),
            (ActivityTraceFlags)flags,
            traceState
        );
    }
}
