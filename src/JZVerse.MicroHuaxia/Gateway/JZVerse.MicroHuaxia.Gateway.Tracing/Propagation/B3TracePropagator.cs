using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace JZVerse.MicroHuaxia.Gateway.Tracing.Propagation;

/// <summary>
/// B3 传播格式
/// </summary>
public enum B3Format
{
    /// <summary>
    /// 单头模式: b3: {trace-id}-{span-id}-{sampled}-{parent-span-id}
    /// </summary>
    Single,

    /// <summary>
    /// 多头模式: X-B3-TraceId, X-B3-SpanId, X-B3-Sampled, X-B3-ParentSpanId
    /// </summary>
    Multi,
}

/// <summary>
/// B3 传播器
/// 支持 Zipkin B3 传播格式（单头和多头模式）
/// https://github.com/openzipkin/b3-propagation
/// </summary>
public sealed class B3TracePropagator : ITracePropagator
{
    // 单头模式
    public const string B3Header = "b3";

    // 多头模式
    public const string TraceIdHeader = "X-B3-TraceId";
    public const string SpanIdHeader = "X-B3-SpanId";
    public const string SampledHeader = "X-B3-Sampled";
    public const string ParentSpanIdHeader = "X-B3-ParentSpanId";
    public const string FlagsHeader = "X-B3-Flags";

    private readonly B3Format _format;

    public string Name => _format == B3Format.Single ? "B3-Single" : "B3-Multi";

    public B3TracePropagator(B3Format format = B3Format.Single)
    {
        _format = format;
    }

    public void Inject(Activity? activity, HttpRequestMessage request)
    {
        if (activity == null)
            return;

        var sampled = (activity.ActivityTraceFlags & ActivityTraceFlags.Recorded) != 0 ? "1" : "0";

        if (_format == B3Format.Single)
        {
            // 单头模式: {trace-id}-{span-id}-{sampled}
            var b3Value = $"{activity.TraceId.ToHexString()}-{activity.SpanId.ToHexString()}-{sampled}";

            request.Headers.Remove(B3Header);
            request.Headers.TryAddWithoutValidation(B3Header, b3Value);
        }
        else
        {
            // 多头模式
            request.Headers.Remove(TraceIdHeader);
            request.Headers.Remove(SpanIdHeader);
            request.Headers.Remove(SampledHeader);
            request.Headers.Remove(ParentSpanIdHeader);

            request.Headers.TryAddWithoutValidation(TraceIdHeader, activity.TraceId.ToHexString());
            request.Headers.TryAddWithoutValidation(SpanIdHeader, activity.SpanId.ToHexString());
            request.Headers.TryAddWithoutValidation(SampledHeader, sampled);

            if (activity.ParentSpanId != default)
            {
                request.Headers.TryAddWithoutValidation(ParentSpanIdHeader, activity.ParentSpanId.ToHexString());
            }
        }
    }

    public PropagationContext Extract(HttpContext context) => Extract(context.Request.Headers);

    public PropagationContext Extract(IHeaderDictionary headers)
    {
        // 优先尝试单头模式
        if (headers.TryGetValue(B3Header, out var b3Values))
        {
            var b3 = b3Values.FirstOrDefault();
            if (!string.IsNullOrEmpty(b3))
            {
                var result = ParseSingleHeader(b3);
                if (result.IsValid)
                    return result;
            }
        }

        // 尝试多头模式
        return ParseMultiHeaders(headers);
    }

    private static PropagationContext ParseSingleHeader(string b3)
    {
        // 格式: {trace-id}-{span-id}-{sampled}[-{parent-span-id}]
        // 或者: {sampled} (仅采样决策)
        // 或者: 0 (不采样)
        // 或者: d (调试模式)

        if (b3 == "0")
        {
            return PropagationContext.Empty; // 不采样
        }

        if (b3 == "d" || b3 == "1")
        {
            return PropagationContext.Empty; // 仅采样决策，没有追踪 ID
        }

        var parts = b3.Split('-');
        if (parts.Length < 2)
        {
            return PropagationContext.Empty;
        }

        var traceIdHex = parts[0];
        var spanIdHex = parts[1];

        // trace-id 可以是 16 或 32 字符
        if (traceIdHex.Length != 16 && traceIdHex.Length != 32)
        {
            return PropagationContext.Empty;
        }

        // 如果是 16 字符，左补零扩展到 32 字符
        if (traceIdHex.Length == 16)
        {
            traceIdHex = "0000000000000000" + traceIdHex;
        }

        // span-id 是 16 字符
        if (spanIdHex.Length != 16)
        {
            return PropagationContext.Empty;
        }

        // 解析采样标志
        var flags = ActivityTraceFlags.None;
        if (parts.Length > 2)
        {
            var sampledPart = parts[2];
            if (sampledPart == "1" || sampledPart == "d")
            {
                flags = ActivityTraceFlags.Recorded;
            }
        }

        return new PropagationContext(
            ActivityTraceId.CreateFromString(traceIdHex.AsSpan()),
            ActivitySpanId.CreateFromString(spanIdHex.AsSpan()),
            flags
        );
    }

    private static PropagationContext ParseMultiHeaders(IHeaderDictionary headers)
    {
        if (!headers.TryGetValue(TraceIdHeader, out var traceIdValues))
        {
            return PropagationContext.Empty;
        }

        if (!headers.TryGetValue(SpanIdHeader, out var spanIdValues))
        {
            return PropagationContext.Empty;
        }

        var traceIdHex = traceIdValues.FirstOrDefault();
        var spanIdHex = spanIdValues.FirstOrDefault();

        if (string.IsNullOrEmpty(traceIdHex) || string.IsNullOrEmpty(spanIdHex))
        {
            return PropagationContext.Empty;
        }

        // trace-id 可以是 16 或 32 字符
        if (traceIdHex.Length != 16 && traceIdHex.Length != 32)
        {
            return PropagationContext.Empty;
        }

        // 如果是 16 字符，左补零扩展到 32 字符
        if (traceIdHex.Length == 16)
        {
            traceIdHex = "0000000000000000" + traceIdHex;
        }

        // span-id 是 16 字符
        if (spanIdHex.Length != 16)
        {
            return PropagationContext.Empty;
        }

        // 解析采样标志
        var flags = ActivityTraceFlags.None;
        if (headers.TryGetValue(SampledHeader, out var sampledValues))
        {
            var sampled = sampledValues.FirstOrDefault();
            if (sampled == "1" || sampled == "true")
            {
                flags = ActivityTraceFlags.Recorded;
            }
        }

        // 检查 debug flag
        if (headers.TryGetValue(FlagsHeader, out var flagsValues))
        {
            var flagsValue = flagsValues.FirstOrDefault();
            if (flagsValue == "1")
            {
                flags = ActivityTraceFlags.Recorded;
            }
        }

        return new PropagationContext(
            ActivityTraceId.CreateFromString(traceIdHex.AsSpan()),
            ActivitySpanId.CreateFromString(spanIdHex.AsSpan()),
            flags
        );
    }
}
