using Microsoft.AspNetCore.Http;

namespace JZVerse.MicroHuaxia.Gateway.Abstractions.Transformation;

/// <summary>
/// 请求转换器接口
/// </summary>
public interface IRequestTransformer
{
    /// <summary>
    /// 转换请求
    /// </summary>
    /// <param name="context">HTTP 上下文</param>
    /// <param name="request">要修改的请求消息</param>
    /// <param name="matchResult">路由匹配结果</param>
    /// <param name="cancellationToken">取消令牌</param>
    ValueTask TransformAsync(
        HttpContext context,
        HttpRequestMessage request,
        Routing.RouteMatchResult matchResult,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 响应转换器接口
/// </summary>
public interface IResponseTransformer
{
    /// <summary>
    /// 转换响应
    /// </summary>
    /// <param name="context">HTTP 上下文</param>
    /// <param name="response">上游响应</param>
    /// <param name="matchResult">路由匹配结果</param>
    /// <param name="cancellationToken">取消令牌</param>
    ValueTask TransformAsync(
        HttpContext context,
        HttpResponseMessage response,
        Routing.RouteMatchResult matchResult,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 请求转换规则
/// </summary>
public sealed record RequestTransformRule
{
    /// <summary>
    /// 要添加的 Header
    /// </summary>
    public Dictionary<string, string> AddHeaders { get; init; } = new();

    /// <summary>
    /// 要移除的 Header
    /// </summary>
    public List<string> RemoveHeaders { get; init; } = [];

    /// <summary>
    /// 要替换的 Header
    /// </summary>
    public Dictionary<string, string> ReplaceHeaders { get; init; } = new();

    /// <summary>
    /// 是否添加 X-Forwarded-* Header
    /// </summary>
    public bool AddForwardedHeaders { get; init; } = true;

    /// <summary>
    /// 是否添加网关请求 ID
    /// </summary>
    public bool AddGatewayRequestId { get; init; } = true;

    /// <summary>
    /// 路径前缀移除
    /// </summary>
    public string? StripPathPrefix { get; init; }

    /// <summary>
    /// 路径前缀添加
    /// </summary>
    public string? AddPathPrefix { get; init; }
}

/// <summary>
/// 响应转换规则
/// </summary>
public sealed record ResponseTransformRule
{
    /// <summary>
    /// 要添加的 Header
    /// </summary>
    public Dictionary<string, string> AddHeaders { get; init; } = new();

    /// <summary>
    /// 要移除的 Header
    /// </summary>
    public List<string> RemoveHeaders { get; init; } = [];

    /// <summary>
    /// 是否添加网关响应时间
    /// </summary>
    public bool AddGatewayTime { get; init; } = true;

    /// <summary>
    /// 是否转换错误响应格式
    /// </summary>
    public bool TransformErrorResponse { get; init; } = true;
}
