using System.Security.Claims;

namespace JZVerse.Business.Abstractions.Authentication;

/// <summary>
/// JWT Token 服务接口
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// 生成访问 Token
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="username">用户名</param>
    /// <param name="roles">用户角色列表</param>
    /// <param name="additionalClaims">额外的 Claims</param>
    /// <returns>访问 Token</returns>
    Task<string> GenerateAccessTokenAsync(
        Guid userId,
        string username,
        IEnumerable<string> roles,
        IDictionary<string, string>? additionalClaims = null);

    /// <summary>
    /// 生成刷新 Token
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>刷新 Token</returns>
    Task<string> GenerateRefreshTokenAsync(Guid userId);

    /// <summary>
    /// 验证访问 Token
    /// </summary>
    /// <param name="token">访问 Token</param>
    /// <returns>验证成功返回 ClaimsPrincipal，失败返回 null</returns>
    Task<ClaimsPrincipal?> ValidateAccessTokenAsync(string token);

    /// <summary>
    /// 验证刷新 Token
    /// </summary>
    /// <param name="refreshToken">刷新 Token</param>
    /// <returns>验证成功返回用户 ID，失败返回 null</returns>
    Task<Guid?> ValidateRefreshTokenAsync(string refreshToken);

    /// <summary>
    /// 撤销刷新 Token
    /// </summary>
    /// <param name="userId">用户 ID</param>
    Task RevokeRefreshTokenAsync(Guid userId);
}
