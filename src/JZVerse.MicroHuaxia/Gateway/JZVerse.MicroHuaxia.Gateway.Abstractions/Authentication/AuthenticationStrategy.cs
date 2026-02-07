namespace JZVerse.MicroHuaxia.Gateway.Abstractions.Authentication;

/// <summary>
/// 认证策略类型
/// </summary>
public enum AuthenticationStrategyType
{
    /// <summary>
    /// JWT Token 认证
    /// </summary>
    Jwt,

    /// <summary>
    /// API Key 认证
    /// </summary>
    ApiKey,

    /// <summary>
    /// Basic 认证
    /// </summary>
    Basic,

    /// <summary>
    /// 自定义认证
    /// </summary>
    Custom
}

/// <summary>
/// 认证策略配置
/// </summary>
public sealed record AuthenticationStrategy
{
    /// <summary>
    /// 策略类型
    /// </summary>
    public AuthenticationStrategyType Type { get; init; }

    /// <summary>
    /// 策略名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 优先级（数字越小优先级越高）
    /// </summary>
    public int Priority { get; init; } = 100;

    /// <summary>
    /// 策略配置
    /// </summary>
    public AuthenticationStrategyConfiguration Configuration { get; init; } = new();

    /// <summary>
    /// 失败模式
    /// </summary>
    public AuthenticationFailureMode FailureMode { get; init; } = AuthenticationFailureMode.Continue;
}

/// <summary>
/// 认证策略详细配置
/// </summary>
public sealed record AuthenticationStrategyConfiguration
{
    // JWT 配置
    /// <summary>
    /// JWT 密钥（支持加密存储，格式：encrypted:xxxxx）
    /// </summary>
    public string? SecretKey { get; init; }

    /// <summary>
    /// JWT 签发者
    /// </summary>
    public string? Issuer { get; init; }

    /// <summary>
    /// JWT 受众
    /// </summary>
    public string? Audience { get; init; }

    /// <summary>
    /// 是否验证生命周期
    /// </summary>
    public bool ValidateLifetime { get; init; } = true;

    /// <summary>
    /// 是否验证签发者
    /// </summary>
    public bool ValidateIssuer { get; init; } = true;

    /// <summary>
    /// 是否验证受众
    /// </summary>
    public bool ValidateAudience { get; init; } = true;

    /// <summary>
    /// 时钟偏移容差
    /// </summary>
    public TimeSpan ClockSkew { get; init; } = TimeSpan.FromMinutes(5);

    // API Key 配置
    /// <summary>
    /// API Key Header 名称
    /// </summary>
    public string? ApiKeyHeaderName { get; init; } = "X-API-Key";

    /// <summary>
    /// API Key Query 参数名称
    /// </summary>
    public string? ApiKeyQueryName { get; init; } = "api_key";

    /// <summary>
    /// API Key 来源
    /// </summary>
    public ApiKeySource ApiKeySource { get; init; } = ApiKeySource.Header;

    /// <summary>
    /// 有效的 API Key 列表（支持加密存储）
    /// </summary>
    public List<string> ValidApiKeys { get; init; } = [];

    // Basic 认证配置
    /// <summary>
    /// Basic 认证用户名
    /// </summary>
    public string? BasicUsername { get; init; }

    /// <summary>
    /// Basic 认证密码（支持加密存储）
    /// </summary>
    public string? BasicPassword { get; init; }

    // 自定义配置
    /// <summary>
    /// 自定义处理器类型名称
    /// </summary>
    public string? CustomHandlerType { get; init; }

    /// <summary>
    /// 自定义配置参数
    /// </summary>
    public Dictionary<string, string> CustomParameters { get; init; } = new();
}

/// <summary>
/// API Key 来源
/// </summary>
public enum ApiKeySource
{
    /// <summary>
    /// 从 Header 获取
    /// </summary>
    Header,

    /// <summary>
    /// 从 Query 参数获取
    /// </summary>
    Query,

    /// <summary>
    /// 两者都尝试
    /// </summary>
    Both
}

/// <summary>
/// 认证失败模式
/// </summary>
public enum AuthenticationFailureMode
{
    /// <summary>
    /// 继续尝试下一个策略
    /// </summary>
    Continue,

    /// <summary>
    /// 立即中断并返回失败
    /// </summary>
    Break
}
