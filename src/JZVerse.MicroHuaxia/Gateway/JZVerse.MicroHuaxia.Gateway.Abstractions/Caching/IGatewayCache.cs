using Microsoft.AspNetCore.Http;

namespace JZVerse.MicroHuaxia.Gateway.Abstractions.Caching;

/// <summary>
/// 网关缓存接口
/// </summary>
public interface IGatewayCache
{
    /// <summary>
    /// 尝试获取缓存响应
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <param name="cancellationToken">取消令牌</param>
    ValueTask<CachedResponse?> TryGetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 设置缓存响应
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <param name="response">缓存响应</param>
    /// <param name="ttl">过期时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    ValueTask SetAsync(string key, CachedResponse response, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>
    /// 移除缓存
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <param name="cancellationToken">取消令牌</param>
    ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清空所有缓存
    /// </summary>
    ValueTask ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 缓存键生成器接口
/// </summary>
public interface ICacheKeyGenerator
{
    /// <summary>
    /// 生成缓存键
    /// </summary>
    /// <param name="context">HTTP 上下文</param>
    /// <param name="cacheConfig">缓存配置</param>
    string GenerateKey(HttpContext context, Routing.RouteCache cacheConfig);
}

/// <summary>
/// 缓存响应
/// </summary>
public sealed record CachedResponse
{
    /// <summary>
    /// 状态码
    /// </summary>
    public int StatusCode { get; init; }

    /// <summary>
    /// 响应头
    /// </summary>
    public Dictionary<string, string[]> Headers { get; init; } = new();

    /// <summary>
    /// 响应体
    /// </summary>
    public byte[] Body { get; init; } = [];

    /// <summary>
    /// 内容类型
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// 缓存时间
    /// </summary>
    public DateTimeOffset CachedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// ETag
    /// </summary>
    public string? ETag { get; init; }

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTimeOffset? LastModified { get; init; }
}
