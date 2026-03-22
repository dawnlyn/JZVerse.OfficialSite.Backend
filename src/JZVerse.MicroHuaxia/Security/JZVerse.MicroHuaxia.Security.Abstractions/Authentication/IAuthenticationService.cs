namespace JZVerse.MicroHuaxia.Security.Authentication;

/// <summary>
/// 认证服务接口
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// 使用 mTLS 证书认证服务身份
    /// </summary>
    /// <param name="clientCertificate">客户端证书</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>认证结果</returns>
    Task<AuthenticationResult> AuthenticateWithMutualTlsAsync(
        byte[] clientCertificate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用 JWT 令牌认证
    /// </summary>
    /// <param name="token">JWT 令牌</param>
    /// <param name="tokenType">令牌类型</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>认证结果</returns>
    Task<AuthenticationResult> AuthenticateWithTokenAsync(
        string token,
        TokenType tokenType = TokenType.AccessToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用 API 密钥认证
    /// </summary>
    /// <param name="apiKey">API 密钥</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>认证结果</returns>
    Task<AuthenticationResult> AuthenticateWithApiKeyAsync(
        string apiKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用用户名密码认证
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="password">密码</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>认证结果</returns>
    Task<AuthenticationResult> AuthenticateWithPasswordAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成服务访问令牌
    /// </summary>
    /// <param name="identity">服务身份</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>令牌信息</returns>
    Task<TokenInfo> GenerateServiceTokenAsync(
        SecurityIdentity identity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 刷新令牌
    /// </summary>
    /// <param name="refreshToken">刷新令牌</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>新令牌信息</returns>
    Task<TokenInfo> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 吊销令牌
    /// </summary>
    /// <param name="token">要吊销的令牌</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> RevokeTokenAsync(
        string token,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 认证结果
/// </summary>
public sealed record AuthenticationResult
{
    /// <summary>
    /// 是否认证成功
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// 安全身份
    /// </summary>
    public SecurityIdentity? Identity { get; init; }

    /// <summary>
    /// 失败原因
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// 认证方式
    /// </summary>
    public AuthenticationMethod Method { get; init; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static AuthenticationResult Success(SecurityIdentity identity, AuthenticationMethod method)
    {
        return new AuthenticationResult
        {
            Succeeded = true,
            Identity = identity,
            Method = method
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static AuthenticationResult Failed(string reason, AuthenticationMethod method = AuthenticationMethod.Unknown)
    {
        return new AuthenticationResult
        {
            Succeeded = false,
            FailureReason = reason,
            Method = method
        };
    }
}

/// <summary>
/// 令牌信息
/// </summary>
public sealed record TokenInfo
{
    /// <summary>
    /// 访问令牌
    /// </summary>
    public string AccessToken { get; init; } = null!;

    /// <summary>
    /// 刷新令牌
    /// </summary>
    public string? RefreshToken { get; init; }

    /// <summary>
    /// 令牌类型
    /// </summary>
    public string TokenType { get; init; } = "Bearer";

    /// <summary>
    /// 过期时间（秒）
    /// </summary>
    public int ExpiresIn { get; init; }

    /// <summary>
    /// 签发时间
    /// </summary>
    public DateTimeOffset IssuedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>
/// 令牌类型
/// </summary>
public enum TokenType
{
    /// <summary>
    /// 访问令牌
    /// </summary>
    AccessToken = 0,

    /// <summary>
    /// 刷新令牌
    /// </summary>
    RefreshToken = 1,

    /// <summary>
    /// 服务令牌
    /// </summary>
    ServiceToken = 2
}
