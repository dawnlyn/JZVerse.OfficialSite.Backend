using Microsoft.AspNetCore.Http;

namespace JZVerse.MicroHuaxia.Gateway.Abstractions.TrafficControl;

/// <summary>
/// 流量镜像服务接口
/// </summary>
public interface ITrafficMirrorService
{
    /// <summary>
    /// 异步镜像请求到目标地址
    /// </summary>
    /// <param name="context">HTTP 上下文</param>
    /// <param name="originalRequest">原始请求消息</param>
    /// <param name="mirrorConfig">镜像配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>镜像任务（Fire-and-forget）</returns>
    Task MirrorAsync(
        HttpContext context,
        HttpRequestMessage originalRequest,
        RouteTrafficMirror mirrorConfig,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 路由流量镜像配置
/// </summary>
public sealed record RouteTrafficMirror
{
    /// <summary>
    /// 是否启用流量镜像
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// 镜像目标地址
    /// </summary>
    public required string TargetAddress { get; init; }

    /// <summary>
    /// 采样百分比（0-100）
    /// </summary>
    public int SamplePercentage { get; init; } = 100;

    /// <summary>
    /// 镜像请求超时时间
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 是否忽略镜像响应
    /// </summary>
    public bool IgnoreResponse { get; init; } = true;

    /// <summary>
    /// 是否在镜像请求中添加标记头
    /// </summary>
    public bool AddMirrorHeader { get; init; } = true;

    /// <summary>
    /// 镜像请求标记头名称
    /// </summary>
    public string MirrorHeaderName { get; init; } = "X-Traffic-Mirror";

    /// <summary>
    /// 镜像请求标记头值
    /// </summary>
    public string MirrorHeaderValue { get; init; } = "true";

    /// <summary>
    /// 是否复制请求体
    /// </summary>
    public bool CopyRequestBody { get; init; } = true;

    /// <summary>
    /// 路径转换（可选，用于转换目标路径）
    /// </summary>
    public string? PathTransform { get; init; }
}
