namespace JZVerse.Business.Central.Security.Database;

/// <summary>
/// 安全中心 SQL 语句
/// </summary>
public static class SecuritySql
{
    #region User 表

    public const string CreateUser = """
        INSERT INTO users (id, username, password_hash, email, phone, status, created_at, updated_at)
        VALUES (@Id, @Username, @PasswordHash, @Email, @Phone, @Status, @CreatedAt, @UpdatedAt)
        """;

    public const string GetUserById = """
        SELECT id, username, password_hash, email, phone, status, created_at, updated_at
        FROM users
        WHERE id = @Id AND deleted_at IS NULL
        """;

    public const string GetUserByUsername = """
        SELECT id, username, password_hash, email, phone, status, created_at, updated_at
        FROM users
        WHERE username = @Username AND deleted_at IS NULL
        """;

    public const string GetUserByEmail = """
        SELECT id, username, password_hash, email, phone, status, created_at, updated_at
        FROM users
        WHERE email = @Email AND deleted_at IS NULL
        """;

    public const string UpdateUserPassword = """
        UPDATE users
        SET password_hash = @PasswordHash, updated_at = @UpdatedAt
        WHERE id = @Id AND deleted_at IS NULL
        """;

    public const string UpdateUserStatus = """
        UPDATE users
        SET status = @Status, updated_at = @UpdatedAt
        WHERE id = @Id AND deleted_at IS NULL
        """;

    public const string SoftDeleteUser = """
        UPDATE users
        SET deleted_at = @DeletedAt, updated_at = @UpdatedAt
        WHERE id = @Id AND deleted_at IS NULL
        """;

    public const string CheckUsernameExists = """
        SELECT EXISTS(
            SELECT 1 FROM users WHERE username = @Username AND deleted_at IS NULL
        )
        """;

    public const string CheckEmailExists = """
        SELECT EXISTS(
            SELECT 1 FROM users WHERE email = @Email AND deleted_at IS NULL
        )
        """;

    #endregion

    #region Role 表

    public const string GetUserRoles = """
        SELECT r.id, r.code, r.name, r.description, r.created_at
        FROM roles r
        INNER JOIN user_roles ur ON r.id = ur.role_id
        WHERE ur.user_id = @UserId AND r.deleted_at IS NULL
        """;

    public const string AssignRoleToUser = """
        INSERT INTO user_roles (user_id, role_id)
        VALUES (@UserId, @RoleId)
        ON CONFLICT (user_id, role_id) DO NOTHING
        """;

    public const string RemoveRoleFromUser = """
        DELETE FROM user_roles
        WHERE user_id = @UserId AND role_id = @RoleId
        """;

    public const string GetRoleByCode = """
        SELECT id, code, name, description, created_at
        FROM roles
        WHERE code = @Code AND deleted_at IS NULL
        """;

    #endregion

    #region Permission 表

    public const string GetUserPermissions = """
        SELECT DISTINCT p.id, p.code, p.name, p.resource, p.action, p.created_at
        FROM permissions p
        INNER JOIN role_permissions rp ON p.id = rp.permission_id
        INNER JOIN user_roles ur ON rp.role_id = ur.role_id
        WHERE ur.user_id = @UserId AND p.deleted_at IS NULL
        """;

    public const string CheckUserHasPermission = """
        SELECT EXISTS(
            SELECT 1
            FROM permissions p
            INNER JOIN role_permissions rp ON p.id = rp.permission_id
            INNER JOIN user_roles ur ON rp.role_id = ur.role_id
            WHERE ur.user_id = @UserId
              AND p.resource = @Resource
              AND p.action = @Action
              AND p.deleted_at IS NULL
        )
        """;

    #endregion

    #region 审计日志

    public const string CreateAuditLog = """
        INSERT INTO security_audit_logs (id, user_id, action, resource, ip_address, user_agent, created_at)
        VALUES (@Id, @UserId, @Action, @Resource, @IpAddress, @UserAgent, @CreatedAt)
        """;

    #endregion
}
