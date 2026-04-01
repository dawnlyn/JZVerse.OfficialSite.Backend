namespace JZVerse.Business.Central.Security.Database.Models;

/// <summary>
/// Token记录实体
/// </summary>
public class SecurityToken
{
    /// <summary>
    /// Token ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 用户ID（不关联具体表，仅存储标识）
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// 访问Token
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// 刷新Token
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Token过期时间
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// 刷新Token过期时间
    /// </summary>
    public DateTime RefreshTokenExpiresAt { get; set; }

    /// <summary>
    /// 是否已撤销
    /// </summary>
    public bool IsRevoked { get; set; }

    /// <summary>
    /// 撤销时间
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 创建IP
    /// </summary>
    public string? CreatedIp { get; set; }

    /// <summary>
    /// 设备信息
    /// </summary>
    public string? UserAgent { get; set; }
}
