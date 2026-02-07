namespace JZVerse.MicroHuaxia.Gateway.Resilience.Abstractions;

/// <summary>
/// 降级处理器接口
/// </summary>
public interface IFallbackHandler
{
    /// <summary>
    /// 处理器名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 是否可以处理此故障
    /// </summary>
    /// <param name="context">弹性上下文</param>
    /// <param name="exception">异常</param>
    bool CanHandle(IGatewayResilienceContext context, Exception exception);

    /// <summary>
    /// 执行降级处理
    /// </summary>
    /// <param name="context">弹性上下文</param>
    /// <param name="exception">异常</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<FallbackResult> HandleAsync(
        IGatewayResilienceContext context,
        Exception exception,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 降级结果
/// </summary>
public sealed record FallbackResult
{
    /// <summary>
    /// HTTP 状态码
    /// </summary>
    public required int StatusCode { get; init; }

    /// <summary>
    /// 响应体
    /// </summary>
    public required byte[] Body { get; init; }

    /// <summary>
    /// 响应头
    /// </summary>
    public Dictionary<string, string[]> Headers { get; init; } = new();

    /// <summary>
    /// 内容类型
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// 降级来源描述
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// 创建静态响应的降级结果
    /// </summary>
    public static FallbackResult FromStatic(int statusCode, string body, string contentType = "application/json")
    {
        return new FallbackResult
        {
            StatusCode = statusCode,
            Body = System.Text.Encoding.UTF8.GetBytes(body),
            ContentType = contentType,
            Source = "static"
        };
    }

    /// <summary>
    /// 创建缓存响应的降级结果
    /// </summary>
    public static FallbackResult FromCache(int statusCode, byte[] body, string? contentType, Dictionary<string, string[]>? headers = null)
    {
        return new FallbackResult
        {
            StatusCode = statusCode,
            Body = body,
            ContentType = contentType,
            Headers = headers ?? new(),
            Source = "cache"
        };
    }
}

/// <summary>
/// 降级处理器注册表接口
/// </summary>
public interface IFallbackHandlerRegistry
{
    /// <summary>
    /// 获取指定名称的降级处理器
    /// </summary>
    IFallbackHandler? GetHandler(string name);

    /// <summary>
    /// 获取所有降级处理器
    /// </summary>
    IEnumerable<IFallbackHandler> GetAllHandlers();

    /// <summary>
    /// 根据上下文和异常选择合适的处理器
    /// </summary>
    IFallbackHandler? SelectHandler(IGatewayResilienceContext context, Exception exception);
}
