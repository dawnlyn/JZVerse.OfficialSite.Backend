namespace JZVerse.Business.Abstractions.Authentication;

/// <summary>
/// 当前请求的用户上下文，从 JWT Claims 中提取
/// </summary>
public interface IUserContext
{
    /// <summary>
    /// 是否已认证
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// 用户 ID
    /// </summary>
    /// <exception cref="Models.AuthenticationException">未认证时访问抛出异常</exception>
    Guid UserId { get; }

    /// <summary>
    /// 用户名
    /// </summary>
    /// <exception cref="Models.AuthenticationException">未认证时访问抛出异常</exception>
    string Username { get; }

    /// <summary>
    /// 用户角色列表
    /// </summary>
    IReadOnlyList<string> Roles { get; }

    /// <summary>
    /// 是否拥有指定角色
    /// </summary>
    /// <param name="role">角色名称</param>
    /// <returns>是否拥有该角色</returns>
    bool HasRole(string role);

    /// <summary>
    /// 是否拥有任意一个指定角色
    /// </summary>
    /// <param name="roles">角色名称列表</param>
    /// <returns>是否拥有其中任意一个角色</returns>
    bool HasAnyRole(params string[] roles);

    /// <summary>
    /// 是否拥有所有指定角色
    /// </summary>
    /// <param name="roles">角色名称列表</param>
    /// <returns>是否拥有所有角色</returns>
    bool HasAllRoles(params string[] roles);

    /// <summary>
    /// 获取自定义 Claim 值
    /// </summary>
    /// <param name="claimType">Claim 类型</param>
    /// <returns>Claim 值，不存在时返回 null</returns>
    string? GetClaim(string claimType);

    /// <summary>
    /// 尝试获取用户 ID（不抛出异常）
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>是否成功获取</returns>
    bool TryGetUserId(out Guid userId);
}
