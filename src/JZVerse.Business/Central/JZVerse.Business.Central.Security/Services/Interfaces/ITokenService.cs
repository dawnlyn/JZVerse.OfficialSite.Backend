using JZVerse.Business.Central.Security.Database.Models;

namespace JZVerse.Business.Central.Security.Services.Interfaces;

/// <summary>
/// Token服务接口
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// 生成Token对（AccessToken + RefreshToken）
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="username">用户名</param>
    /// <param name="claims">额外声明</param>
    /// <param name="ipAddress">IP地址</param>
    /// <param name="userAgent">用户代理</param>
    /// <returns>Token生成结果</returns>
    Task<TokenResult> GenerateTokensAsync(
        Guid userId, 
        string username, 
        Dictionary<string, string>? claims = null,
        string? ipAddress = null,
        string? userAgent = null);

    /// <summary>
    /// 验证Access Token
    /// </summary>
    /// <param name="token">Token字符串</param>
    /// <returns>验证结果</returns>
    Task<TokenValidationResult> ValidateTokenAsync(string token);

    /// <summary>
    /// 验证Refresh Token并生成新的Token对
    /// </summary>
    /// <param name="refreshToken">Refresh Token</param>
    /// <returns>新的Token结果</returns>
    Task<TokenResult> RefreshTokensAsync(string refreshToken);

    /// <summary>
    /// 撤销Token
    /// </summary>
    /// <param name="token">Token字符串</param>
    /// <returns>是否成功</returns>
    Task<bool> RevokeTokenAsync(string token);

    /// <summary>
    /// 根据用户ID撤销所有Token
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>是否成功</returns>
    Task<bool> RevokeAllUserTokensAsync(Guid userId);

    /// <summary>
    /// 清理过期Token
    /// </summary>
    /// <returns>清理数量</returns>
    Task<int> CleanExpiredTokensAsync();

    /// <summary>
    /// 健康检查
    /// </summary>
    Task<bool> HealthCheckAsync();
}

/// <summary>
/// Token生成结果
/// </summary>
public class TokenResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Access Token
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// Refresh Token
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Token类型
    /// </summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// 过期时间（秒）
    /// </summary>
    public int ExpiresIn { get; set; }

    /// <summary>
    /// Token过期时间
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    public static TokenResult SuccessResult(string accessToken, string refreshToken, int expiresIn, DateTime expiresAt)
    {
        return new TokenResult
        {
            Success = true,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = expiresIn,
            ExpiresAt = expiresAt
        };
    }

    public static TokenResult FailureResult(string errorMessage)
    {
        return new TokenResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>
/// Token验证结果
/// </summary>
public class TokenValidationResult
{
    /// <summary>
    /// 是否有效
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 用户ID
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Token声明
    /// </summary>
    public Dictionary<string, string>? Claims { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    public static TokenValidationResult Valid(Guid userId, string username, Dictionary<string, string>? claims = null)
    {
        return new TokenValidationResult
        {
            IsValid = true,
            UserId = userId,
            Username = username,
            Claims = claims
        };
    }

    public static TokenValidationResult Invalid(string errorMessage)
    {
        return new TokenValidationResult
        {
            IsValid = false,
            ErrorMessage = errorMessage
        };
    }
}
