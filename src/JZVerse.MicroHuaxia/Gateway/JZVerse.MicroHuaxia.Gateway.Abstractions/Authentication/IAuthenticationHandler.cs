using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace JZVerse.MicroHuaxia.Gateway.Abstractions.Authentication;

/// <summary>
/// 认证处理器接口
/// </summary>
public interface IAuthenticationHandler
{
    /// <summary>
    /// 处理器名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 处理器优先级（数字越小优先级越高）
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// 执行认证
    /// </summary>
    /// <param name="context">HTTP 上下文</param>
    /// <param name="configuration">认证配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    ValueTask<AuthenticationResult> AuthenticateAsync(
        HttpContext context,
        AuthenticationStrategyConfiguration configuration,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 认证管道接口
/// </summary>
public interface IAuthenticationPipeline
{
    /// <summary>
    /// 执行认证
    /// </summary>
    /// <param name="context">HTTP 上下文</param>
    /// <param name="config">路由认证配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<AuthenticationResult> AuthenticateAsync(
        HttpContext context,
        Routing.RouteAuthentication config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 注册认证处理器
    /// </summary>
    /// <param name="handler">认证处理器</param>
    void RegisterHandler(IAuthenticationHandler handler);

    /// <summary>
    /// 获取所有已注册的处理器
    /// </summary>
    IReadOnlyList<IAuthenticationHandler> GetHandlers();
}

/// <summary>
/// 认证结果
/// </summary>
public sealed record AuthenticationResult
{
    /// <summary>
    /// 是否认证成功
    /// </summary>
    public bool IsAuthenticated { get; init; }

    /// <summary>
    /// 认证失败原因
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// 认证用户标识
    /// </summary>
    public ClaimsPrincipal? Principal { get; init; }

    /// <summary>
    /// 使用的认证方案
    /// </summary>
    public string? AuthenticationScheme { get; init; }

    /// <summary>
    /// 认证时间
    /// </summary>
    public DateTimeOffset AuthenticatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 创建成功的认证结果
    /// </summary>
    public static AuthenticationResult Success(ClaimsPrincipal principal, string scheme) => new()
    {
        IsAuthenticated = true,
        Principal = principal,
        AuthenticationScheme = scheme
    };

    /// <summary>
    /// 创建失败的认证结果
    /// </summary>
    public static AuthenticationResult Failure(string reason) => new()
    {
        IsAuthenticated = false,
        FailureReason = reason
    };

    /// <summary>
    /// 跳过认证（用于无需认证的路由）
    /// </summary>
    public static AuthenticationResult Skip() => new()
    {
        IsAuthenticated = true,
        AuthenticationScheme = "Skip"
    };
}
