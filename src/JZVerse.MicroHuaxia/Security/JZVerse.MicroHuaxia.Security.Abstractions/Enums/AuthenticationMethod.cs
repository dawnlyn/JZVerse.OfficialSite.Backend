namespace JZVerse.MicroHuaxia.Security;

/// <summary>
/// 认证方式枚举
/// </summary>
public enum AuthenticationMethod
{
    /// <summary>
    /// 未知方式
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 双向 TLS (mTLS)
    /// </summary>
    MutualTls = 1,

    /// <summary>
    /// JWT 令牌
    /// </summary>
    JwtToken = 2,

    /// <summary>
    /// SPIFFE X.509 SVID
    /// </summary>
    SpiffeX509 = 3,

    /// <summary>
    /// SPIFFE JWT SVID
    /// </summary>
    SpiffeJwt = 4,

    /// <summary>
    /// API 密钥
    /// </summary>
    ApiKey = 5,

    /// <summary>
    /// OAuth2/OIDC
    /// </summary>
    OAuth2 = 6,

    /// <summary>
    /// 用户名密码
    /// </summary>
    UsernamePassword = 7,

    /// <summary>
    /// 多因素认证
    /// </summary>
    MultiFactor = 8
}
