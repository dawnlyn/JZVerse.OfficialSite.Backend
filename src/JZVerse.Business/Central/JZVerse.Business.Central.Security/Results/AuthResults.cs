namespace JZVerse.Business.Central.Security.Results;

/// <summary>
/// 登录响应
/// </summary>
public sealed record ResultLoginToken
{
    /// <summary>
    /// 访问令牌
    /// </summary>
    public string AccessToken { get; init; } = "";

    /// <summary>
    /// 刷新令牌
    /// </summary>
    public string RefreshToken { get; init; } = "";

    /// <summary>
    /// Token 类型
    /// </summary>
    public string TokenType { get; init; } = "Bearer";

    /// <summary>
    /// 过期时间（秒）
    /// </summary>
    public int ExpiresIn { get; init; }

    /// <summary>
    /// 用户信息
    /// </summary>
    public ResultUserInfo? User { get; init; }
}

/// <summary>
/// 用户信息响应
/// </summary>
public sealed record ResultUserInfo
{
    /// <summary>
    /// 用户 ID
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// 用户名
    /// </summary>
    public string Username { get; init; } = "";

    /// <summary>
    /// 邮箱
    /// </summary>
    public string Email { get; init; } = "";

    /// <summary>
    /// 角色列表
    /// </summary>
    public IReadOnlyList<string> Roles { get; init; } = [];
}

/// <summary>
/// 用户 ID 响应
/// </summary>
public sealed record ResultUserId(Guid UserId);
