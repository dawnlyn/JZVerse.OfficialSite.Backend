using Microsoft.AspNetCore.Http;

namespace JZVerse.MicroHuaxia.Gateway.Abstractions.TrafficControl;

/// <summary>
/// 流量染色服务接口
/// </summary>
public interface ITrafficColoringService
{
    /// <summary>
    /// 对请求应用流量染色
    /// </summary>
    /// <param name="context">HTTP 上下文</param>
    /// <param name="coloringConfig">染色配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>染色结果</returns>
    Task<TrafficColoringResult> ApplyColoringAsync(
        HttpContext context,
        RouteTrafficColoring coloringConfig,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 流量染色结果
/// </summary>
public sealed record TrafficColoringResult
{
    /// <summary>
    /// 染色标签集合
    /// </summary>
    public HashSet<string> Tags { get; init; } = [];

    /// <summary>
    /// 匹配的规则名称
    /// </summary>
    public string? MatchedRule { get; init; }

    /// <summary>
    /// 是否有染色标签
    /// </summary>
    public bool HasTags => Tags.Count > 0;

    /// <summary>
    /// 创建空结果
    /// </summary>
    public static TrafficColoringResult Empty() => new();

    /// <summary>
    /// 创建带标签的结果
    /// </summary>
    public static TrafficColoringResult WithTags(IEnumerable<string> tags, string? matchedRule = null) => new()
    {
        Tags = [..tags],
        MatchedRule = matchedRule
    };
}

/// <summary>
/// 路由流量染色配置
/// </summary>
public sealed record RouteTrafficColoring
{
    /// <summary>
    /// 是否启用流量染色
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// 染色规则列表
    /// </summary>
    public List<TrafficColoringRule> Rules { get; init; } = [];
}

/// <summary>
/// 流量染色规则
/// </summary>
public sealed record TrafficColoringRule
{
    /// <summary>
    /// 规则名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 染色类型
    /// </summary>
    public TrafficColoringType Type { get; init; } = TrafficColoringType.Percentage;

    /// <summary>
    /// 染色标签
    /// </summary>
    public required string Tag { get; init; }

    /// <summary>
    /// 百分比分流比例（0-100）
    /// 当 Type = Percentage 时使用
    /// </summary>
    public int Percentage { get; init; } = 10;

    /// <summary>
    /// 哈希源（用于百分比分流）
    /// </summary>
    public TrafficColoringHashSource HashSource { get; init; } = TrafficColoringHashSource.ClientIp;

    /// <summary>
    /// Header 名称
    /// 当 Type = HeaderMatch 时使用
    /// </summary>
    public string? HeaderName { get; init; }

    /// <summary>
    /// Header 值（精确匹配）
    /// </summary>
    public string? HeaderValue { get; init; }

    /// <summary>
    /// Header 值模式（正则匹配）
    /// </summary>
    public string? HeaderPattern { get; init; }

    /// <summary>
    /// 用户白名单
    /// 当 Type = UserWhitelist 时使用
    /// </summary>
    public HashSet<string> UserIds { get; init; } = [];

    /// <summary>
    /// 规则优先级（数字越小优先级越高）
    /// </summary>
    public int Priority { get; init; } = 100;
}

/// <summary>
/// 流量染色类型
/// </summary>
public enum TrafficColoringType
{
    /// <summary>
    /// 百分比分流
    /// </summary>
    Percentage,

    /// <summary>
    /// Header 匹配
    /// </summary>
    HeaderMatch,

    /// <summary>
    /// 用户白名单
    /// </summary>
    UserWhitelist,

    /// <summary>
    /// Query 参数匹配
    /// </summary>
    QueryMatch,

    /// <summary>
    /// Cookie 匹配
    /// </summary>
    CookieMatch
}

/// <summary>
/// 百分比分流的哈希源
/// </summary>
public enum TrafficColoringHashSource
{
    /// <summary>
    /// 客户端 IP
    /// </summary>
    ClientIp,

    /// <summary>
    /// 用户 ID
    /// </summary>
    UserId,

    /// <summary>
    /// 请求 ID
    /// </summary>
    RequestId,

    /// <summary>
    /// Session ID
    /// </summary>
    SessionId
}
