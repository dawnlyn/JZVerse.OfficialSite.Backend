namespace JZVerse.Business.Infrastructure.Authentication;

/// <summary>
/// JWT 配置选项
/// </summary>
public sealed class JwtOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Authentication:Jwt";

    /// <summary>
    /// JWT 密钥（明文或 SM2 加密后的密文）
    /// </summary>
    public string SecretKey { get; set; } = "";

    /// <summary>
    /// 是否启用 SM2 加密 SecretKey
    /// </summary>
    public bool EnableSm2Encryption { get; set; }

    /// <summary>
    /// Payload 是否启用 SM2 加密
    /// </summary>
    public bool EnablePayloadEncryption { get; set; }

    /// <summary>
    /// 签发者
    /// </summary>
    public string Issuer { get; set; } = "jzverse";

    /// <summary>
    /// 受众
    /// </summary>
    public string Audience { get; set; } = "jzverse-api";

    /// <summary>
    /// Token 过期时间（分钟）
    /// </summary>
    public int ExpirationMinutes { get; set; } = 120;

    /// <summary>
    /// Refresh Token 过期时间（天）
    /// </summary>
    public int RefreshTokenExpirationDays { get; set; } = 7;

    /// <summary>
    /// 时钟偏移容差（秒）
    /// </summary>
    public int ClockSkewSeconds { get; set; } = 300;
}
