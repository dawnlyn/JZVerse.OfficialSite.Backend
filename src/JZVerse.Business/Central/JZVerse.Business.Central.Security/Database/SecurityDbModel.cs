namespace JZVerse.Business.Central.Security.Database;

/// <summary>
/// 用户实体
/// </summary>
public sealed record UserEntity
{
    /// <summary>
    /// 用户唯一标识
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 用户名
    /// </summary>
    public string Username { get; init; } = "";

    /// <summary>
    /// 密码哈希值
    /// </summary>
    public string PasswordHash { get; init; } = "";

    /// <summary>
    /// 邮箱地址
    /// </summary>
    public string Email { get; init; } = "";

    /// <summary>
    /// 手机号码
    /// </summary>
    public string? Phone { get; init; }

    /// <summary>
    /// 用户状态
    /// </summary>
    public int Status { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// 软删除时间
    /// </summary>
    public DateTimeOffset? DeletedAt { get; init; }
}

/// <summary>
/// 角色实体
/// </summary>
public sealed record RoleEntity
{
    /// <summary>
    /// 角色唯一标识
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 角色编码（用于程序判断）
    /// </summary>
    public string Code { get; init; } = "";

    /// <summary>
    /// 角色名称
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// 角色描述
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// 软删除时间
    /// </summary>
    public DateTimeOffset? DeletedAt { get; init; }
}

/// <summary>
/// 权限实体
/// </summary>
public sealed record PermissionEntity
{
    /// <summary>
    /// 权限唯一标识
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 权限编码（用于程序判断）
    /// </summary>
    public string Code { get; init; } = "";

    /// <summary>
    /// 权限名称
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// 资源标识（控制的资源对象）
    /// </summary>
    public string Resource { get; init; } = "";

    /// <summary>
    /// 操作标识（read/write/delete 等）
    /// </summary>
    public string Action { get; init; } = "";

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// 软删除时间
    /// </summary>
    public DateTimeOffset? DeletedAt { get; init; }
}

/// <summary>
/// 用户状态
/// </summary>
public enum UserStatus
{
    /// <summary>
    /// 正常
    /// </summary>
    Active = 1,

    /// <summary>
    /// 禁用
    /// </summary>
    Disabled = 2
}
