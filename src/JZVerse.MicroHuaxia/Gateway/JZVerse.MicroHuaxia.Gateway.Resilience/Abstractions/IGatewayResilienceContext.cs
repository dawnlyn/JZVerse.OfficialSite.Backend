using JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;
using Microsoft.AspNetCore.Http;

namespace JZVerse.MicroHuaxia.Gateway.Resilience.Abstractions;

/// <summary>
/// Gateway 弹性执行上下文
/// </summary>
public interface IGatewayResilienceContext
{
    /// <summary>
    /// 当前路由配置
    /// </summary>
    GatewayRoute Route { get; }

    /// <summary>
    /// HTTP 上下文
    /// </summary>
    HttpContext HttpContext { get; }

    /// <summary>
    /// 路由匹配结果
    /// </summary>
    RouteMatchResult MatchResult { get; }

    /// <summary>
    /// 目标服务地址
    /// </summary>
    string? TargetAddress { get; set; }

    /// <summary>
    /// 执行尝试次数（包括首次）
    /// </summary>
    int AttemptCount { get; set; }

    /// <summary>
    /// 是否被熔断
    /// </summary>
    bool WasCircuitBroken { get; set; }

    /// <summary>
    /// 是否执行了降级
    /// </summary>
    bool FallbackExecuted { get; set; }

    /// <summary>
    /// 降级类型（如果执行了降级）
    /// </summary>
    string? FallbackType { get; set; }

    /// <summary>
    /// 是否被舱壁拒绝
    /// </summary>
    bool WasBulkheadRejected { get; set; }

    /// <summary>
    /// 最后一次错误
    /// </summary>
    Exception? LastException { get; set; }

    /// <summary>
    /// 扩展属性
    /// </summary>
    IDictionary<string, object> Properties { get; }
}

/// <summary>
/// Gateway 弹性执行上下文实现
/// </summary>
public sealed class GatewayResilienceContext : IGatewayResilienceContext
{
    public required GatewayRoute Route { get; init; }
    public required HttpContext HttpContext { get; init; }
    public required RouteMatchResult MatchResult { get; init; }
    public string? TargetAddress { get; set; }
    public int AttemptCount { get; set; } = 1;
    public bool WasCircuitBroken { get; set; }
    public bool FallbackExecuted { get; set; }
    public string? FallbackType { get; set; }
    public bool WasBulkheadRejected { get; set; }
    public Exception? LastException { get; set; }
    public IDictionary<string, object> Properties { get; } = new Dictionary<string, object>();

    /// <summary>
    /// 将弹性上下文信息写入 HttpContext.Items
    /// </summary>
    public void WriteToHttpContext()
    {
        HttpContext.Items["RetryCount"] = AttemptCount - 1; // 重试次数不包括首次
        HttpContext.Items["WasCircuitBroken"] = WasCircuitBroken;
        HttpContext.Items["FallbackExecuted"] = FallbackExecuted;
        HttpContext.Items["FallbackType"] = FallbackType;
        HttpContext.Items["WasBulkheadRejected"] = WasBulkheadRejected;

        if (LastException != null)
        {
            HttpContext.Items["ErrorMessage"] = LastException.Message;
        }
    }
}
