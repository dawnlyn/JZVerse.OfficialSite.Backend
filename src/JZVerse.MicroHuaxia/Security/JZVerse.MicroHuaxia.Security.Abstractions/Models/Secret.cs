namespace JZVerse.MicroHuaxia.Security;

/// <summary>
/// 密钥信息
/// </summary>
public sealed record Secret
{
    /// <summary>
    /// 密钥 ID
    /// </summary>
    public string SecretId { get; init; } = null!;

    /// <summary>
    /// 密钥名称
    /// </summary>
    public string Name { get; init; } = null!;

    /// <summary>
    /// 密钥类型
    /// </summary>
    public SecretType Type { get; init; }

    /// <summary>
    /// 密钥版本
    /// </summary>
    public int Version { get; init; } = 1;

    /// <summary>
    /// 加密后的密钥值
    /// </summary>
    public string EncryptedValue { get; init; } = null!;

    /// <summary>
    /// 密钥描述
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 所属命名空间
    /// </summary>
    public string? Namespace { get; init; }

    /// <summary>
    /// 关联服务
    /// </summary>
    public string? ServiceName { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// 最后轮换时间
    /// </summary>
    public DateTimeOffset? LastRotatedAt { get; init; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// 扩展标签
    /// </summary>
    public IReadOnlyDictionary<string, string> Labels { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// 密钥类型
/// </summary>
public enum SecretType
{
    /// <summary>
    /// 未知类型
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// API 密钥
    /// </summary>
    ApiKey = 1,

    /// <summary>
    /// 数据库连接字符串
    /// </summary>
    DatabaseConnectionString = 2,

    /// <summary>
    /// TLS 证书
    /// </summary>
    TlsCertificate = 3,

    /// <summary>
    /// 私钥
    /// </summary>
    PrivateKey = 4,

    /// <summary>
    /// 密码
    /// </summary>
    Password = 5,

    /// <summary>
    /// 令牌
    /// </summary>
    Token = 6,

    /// <summary>
    /// 自定义
    /// </summary>
    Custom = 99
}
