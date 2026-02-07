using Microsoft.AspNetCore.Http;

namespace JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;

/// <summary>
/// 请求转发器接口
/// </summary>
public interface IRequestForwarder
{
    /// <summary>
    /// 转发请求
    /// </summary>
    /// <param name="context">HTTP 上下文</param>
    /// <param name="matchResult">路由匹配结果</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ForwardAsync(HttpContext context, RouteMatchResult matchResult, CancellationToken cancellationToken = default);
}

/// <summary>
/// 协议特定的请求转发器接口
/// </summary>
public interface IProtocolForwarder
{
    /// <summary>
    /// 支持的协议类型
    /// </summary>
    string Protocol { get; }

    /// <summary>
    /// 是否可以处理此请求
    /// </summary>
    bool CanHandle(HttpContext context);

    /// <summary>
    /// 转发请求
    /// </summary>
    Task ForwardAsync(HttpContext context, RouteMatchResult matchResult, CancellationToken cancellationToken = default);
}
