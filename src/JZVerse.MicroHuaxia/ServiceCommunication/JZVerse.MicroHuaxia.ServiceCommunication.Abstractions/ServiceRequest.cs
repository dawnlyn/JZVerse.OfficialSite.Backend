namespace JZVerse.MicroHuaxia.ServiceCommunication.Abstractions;

/// <summary>
/// 服务请求
/// </summary>
public sealed record ServiceRequest
{
    /// <summary>
    /// 请求路径（HTTP: /api/users, gRPC: UserService/GetUser）
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// HTTP 方法（仅 HTTP 协议使用）
    /// </summary>
    public HttpMethod? Method { get; init; }

    /// <summary>
    /// 请求体
    /// </summary>
    public object? Body { get; init; }

    /// <summary>
    /// 请求头
    /// </summary>
    public Dictionary<string, string> Headers { get; init; } = [];

    /// <summary>
    /// 查询参数（仅 HTTP 协议使用）
    /// </summary>
    public Dictionary<string, string> QueryParams { get; init; } = [];

    /// <summary>
    /// 超时时间
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// 请求优先级
    /// </summary>
    public RequestPriority Priority { get; init; } = RequestPriority.Normal;

    /// <summary>
    /// 是否重试
    /// </summary>
    public bool EnableRetry { get; init; } = true;

    /// <summary>
    /// 指定服务版本
    /// </summary>
    public string? TargetVersion { get; init; }

    /// <summary>
    /// 指定服务标签
    /// </summary>
    public HashSet<string>? TargetTags { get; init; }
}

/// <summary>
/// 请求优先级
/// </summary>
public enum RequestPriority
{
    Low,
    Normal,
    High,
    Critical
}
