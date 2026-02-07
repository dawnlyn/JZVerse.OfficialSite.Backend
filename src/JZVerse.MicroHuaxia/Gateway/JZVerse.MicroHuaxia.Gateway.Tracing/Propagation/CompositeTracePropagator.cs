using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace JZVerse.MicroHuaxia.Gateway.Tracing.Propagation;

/// <summary>
/// 组合传播器
/// 支持同时使用多种传播格式，按优先级提取追踪上下文
/// </summary>
public sealed class CompositeTracePropagator : ITracePropagator
{
    private readonly IReadOnlyList<ITracePropagator> _propagators;
    private readonly ITracePropagator _primaryPropagator;

    public string Name => "Composite";

    /// <summary>
    /// 创建组合传播器
    /// </summary>
    /// <param name="propagators">传播器列表，按优先级排序（第一个优先级最高）</param>
    /// <exception cref="ArgumentException">传播器列表不能为空</exception>
    public CompositeTracePropagator(IEnumerable<ITracePropagator> propagators)
    {
        var list = propagators?.ToList() ?? throw new ArgumentNullException(nameof(propagators));

        if (list.Count == 0)
        {
            throw new ArgumentException("At least one propagator is required", nameof(propagators));
        }

        _propagators = list;
        _primaryPropagator = list[0];
    }

    /// <summary>
    /// 创建默认的组合传播器（W3C + B3）
    /// </summary>
    public static CompositeTracePropagator CreateDefault() =>
        new(
            [
                new W3CTracePropagator(),
                new B3TracePropagator(B3Format.Single),
                new B3TracePropagator(B3Format.Multi),
            ]
        );

    /// <summary>
    /// 注入追踪上下文
    /// 使用所有传播器注入，确保下游服务无论使用哪种格式都能接收
    /// </summary>
    public void Inject(Activity? activity, HttpRequestMessage request)
    {
        if (activity == null)
            return;

        foreach (var propagator in _propagators)
        {
            propagator.Inject(activity, request);
        }
    }

    /// <summary>
    /// 提取追踪上下文
    /// 按优先级尝试各传播器，返回第一个成功提取的结果
    /// </summary>
    public PropagationContext Extract(HttpContext context) => Extract(context.Request.Headers);

    public PropagationContext Extract(IHeaderDictionary headers)
    {
        foreach (var propagator in _propagators)
        {
            var result = propagator.Extract(headers);
            if (result.IsValid)
            {
                return result;
            }
        }

        return PropagationContext.Empty;
    }
}
