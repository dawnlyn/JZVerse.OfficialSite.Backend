using System.Diagnostics;
using JZVerse.MicroHuaxia.Gateway.Tracing.Configuration;
using Microsoft.AspNetCore.Http;

namespace JZVerse.MicroHuaxia.Gateway.Tracing.Propagation;

/// <summary>
/// 自定义 Header 传播器
/// 支持使用自定义的 Header 名称传播追踪上下文
/// </summary>
public sealed class CustomTracePropagator : ITracePropagator
{
    private readonly CustomHeaderOptions _options;

    public string Name => "Custom";

    public CustomTracePropagator(CustomHeaderOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public void Inject(Activity? activity, HttpRequestMessage request)
    {
        if (activity == null)
            return;

        // 注入 Trace ID
        request.Headers.Remove(_options.TraceIdHeader);
        request.Headers.TryAddWithoutValidation(_options.TraceIdHeader, activity.TraceId.ToHexString());

        // 注入 Span ID
        request.Headers.Remove(_options.SpanIdHeader);
        request.Headers.TryAddWithoutValidation(_options.SpanIdHeader, activity.SpanId.ToHexString());

        // 注入 Parent Span ID（如果有）
        if (activity.ParentSpanId != default && !string.IsNullOrEmpty(_options.ParentSpanIdHeader))
        {
            request.Headers.Remove(_options.ParentSpanIdHeader);
            request.Headers.TryAddWithoutValidation(_options.ParentSpanIdHeader, activity.ParentSpanId.ToHexString());
        }

        // 注入采样标志（如果配置了）
        if (!string.IsNullOrEmpty(_options.SampledHeader))
        {
            var sampled = (activity.ActivityTraceFlags & ActivityTraceFlags.Recorded) != 0 ? "1" : "0";
            request.Headers.Remove(_options.SampledHeader);
            request.Headers.TryAddWithoutValidation(_options.SampledHeader, sampled);
        }
    }

    public PropagationContext Extract(HttpContext context) => Extract(context.Request.Headers);

    public PropagationContext Extract(IHeaderDictionary headers)
    {
        // 获取 Trace ID
        if (!headers.TryGetValue(_options.TraceIdHeader, out var traceIdValues))
        {
            return PropagationContext.Empty;
        }

        var traceIdHex = traceIdValues.FirstOrDefault();
        if (string.IsNullOrEmpty(traceIdHex))
        {
            return PropagationContext.Empty;
        }

        // 获取 Span ID
        if (!headers.TryGetValue(_options.SpanIdHeader, out var spanIdValues))
        {
            return PropagationContext.Empty;
        }

        var spanIdHex = spanIdValues.FirstOrDefault();
        if (string.IsNullOrEmpty(spanIdHex))
        {
            return PropagationContext.Empty;
        }

        // 标准化 trace-id 长度
        if (traceIdHex.Length < 32)
        {
            traceIdHex = traceIdHex.PadLeft(32, '0');
        }
        else if (traceIdHex.Length > 32)
        {
            traceIdHex = traceIdHex[..32];
        }

        // 标准化 span-id 长度
        if (spanIdHex.Length < 16)
        {
            spanIdHex = spanIdHex.PadLeft(16, '0');
        }
        else if (spanIdHex.Length > 16)
        {
            spanIdHex = spanIdHex[..16];
        }

        // 解析采样标志
        var flags = ActivityTraceFlags.None;
        if (!string.IsNullOrEmpty(_options.SampledHeader) && headers.TryGetValue(_options.SampledHeader, out var sampledValues))
        {
            var sampled = sampledValues.FirstOrDefault();
            if (sampled == "1" || sampled?.Equals("true", StringComparison.OrdinalIgnoreCase) == true)
            {
                flags = ActivityTraceFlags.Recorded;
            }
        }

        try
        {
            return new PropagationContext(
                ActivityTraceId.CreateFromString(traceIdHex.AsSpan()),
                ActivitySpanId.CreateFromString(spanIdHex.AsSpan()),
                flags
            );
        }
        catch
        {
            return PropagationContext.Empty;
        }
    }
}
