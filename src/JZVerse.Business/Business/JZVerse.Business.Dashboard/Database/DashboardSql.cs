namespace JZVerse.Business.Dashboard.Database;

/// <summary>
/// 后台管理 SQL 语句
/// </summary>
internal static class DashboardSql
{
    #region System Config

    public const string CreateConfig = """
        INSERT INTO system_configs (id, config_key, config_value, description, category, is_public, created_at)
        VALUES (@Id, @ConfigKey, @ConfigValue, @Description, @Category, @IsPublic, @CreatedAt)
        ON CONFLICT (config_key) DO UPDATE SET
            config_value = EXCLUDED.config_value,
            description = EXCLUDED.description,
            updated_at = NOW()
        """;

    public const string UpdateConfig = """
        UPDATE system_configs
        SET config_value = @ConfigValue, description = @Description, updated_at = @UpdatedAt
        WHERE config_key = @ConfigKey
        """;

    public const string GetConfigByKey = """
        SELECT id, config_key, config_value, description, category, is_public, created_at, updated_at
        FROM system_configs WHERE config_key = @ConfigKey
        """;

    public const string GetConfigsByCategory = """
        SELECT id, config_key, config_value, description, category, is_public, created_at, updated_at
        FROM system_configs WHERE category = @Category ORDER BY config_key
        """;

    public const string GetPublicConfigs = """
        SELECT id, config_key, config_value, description, category, is_public, created_at, updated_at
        FROM system_configs WHERE is_public = true ORDER BY category, config_key
        """;

    public const string GetAllConfigs = """
        SELECT id, config_key, config_value, description, category, is_public, created_at, updated_at
        FROM system_configs ORDER BY category, config_key
        """;

    public const string DeleteConfig = """
        DELETE FROM system_configs WHERE config_key = @ConfigKey
        """;

    #endregion

    #region Menu

    public const string CreateMenu = """
        INSERT INTO menus (id, parent_id, name, icon, path, component, permission, sort_order, is_visible, is_enabled, created_at)
        VALUES (@Id, @ParentId, @Name, @Icon, @Path, @Component, @Permission, @SortOrder, @IsVisible, @IsEnabled, @CreatedAt)
        """;

    public const string UpdateMenu = """
        UPDATE menus
        SET name = @Name, icon = @Icon, path = @Path, component = @Component,
            permission = @Permission, sort_order = @SortOrder, is_visible = @IsVisible,
            is_enabled = @IsEnabled, updated_at = @UpdatedAt
        WHERE id = @Id
        """;

    public const string GetMenuById = """
        SELECT id, parent_id, name, icon, path, component, permission, sort_order, is_visible, is_enabled, created_at, updated_at
        FROM menus WHERE id = @Id
        """;

    public const string GetAllMenus = """
        SELECT id, parent_id, name, icon, path, component, permission, sort_order, is_visible, is_enabled, created_at, updated_at
        FROM menus WHERE is_enabled = true ORDER BY sort_order
        """;

    public const string GetMenusByParentId = """
        SELECT id, parent_id, name, icon, path, component, permission, sort_order, is_visible, is_enabled, created_at, updated_at
        FROM menus WHERE parent_id = @ParentId AND is_enabled = true ORDER BY sort_order
        """;

    public const string GetRootMenus = """
        SELECT id, parent_id, name, icon, path, component, permission, sort_order, is_visible, is_enabled, created_at, updated_at
        FROM menus WHERE parent_id IS NULL AND is_enabled = true ORDER BY sort_order
        """;

    public const string DeleteMenu = """
        DELETE FROM menus WHERE id = @Id
        """;

    #endregion

    #region Role

    public const string CreateRole = """
        INSERT INTO roles (id, code, name, description, is_system, is_enabled, created_at)
        VALUES (@Id, @Code, @Name, @Description, @IsSystem, @IsEnabled, @CreatedAt)
        """;

    public const string UpdateRole = """
        UPDATE roles
        SET name = @Name, description = @Description, is_enabled = @IsEnabled, updated_at = @UpdatedAt
        WHERE id = @Id AND is_system = false
        """;

    public const string GetRoleById = """
        SELECT id, code, name, description, is_system, is_enabled, created_at, updated_at
        FROM roles WHERE id = @Id
        """;

    public const string GetRoleByCode = """
        SELECT id, code, name, description, is_system, is_enabled, created_at, updated_at
        FROM roles WHERE code = @Code
        """;

    public const string GetAllRoles = """
        SELECT id, code, name, description, is_system, is_enabled, created_at, updated_at
        FROM roles ORDER BY is_system DESC, created_at
        """;

    public const string DeleteRole = """
        DELETE FROM roles WHERE id = @Id AND is_system = false
        """;

    public const string AssignMenusToRole = """
        INSERT INTO role_menus (role_id, menu_id, created_at)
        VALUES (@RoleId, @MenuId, @CreatedAt)
        ON CONFLICT (role_id, menu_id) DO NOTHING
        """;

    public const string RemoveMenusFromRole = """
        DELETE FROM role_menus WHERE role_id = @RoleId
        """;

    public const string GetRoleMenus = """
        SELECT m.id, m.parent_id, m.name, m.icon, m.path, m.component, m.permission, m.sort_order, m.is_visible, m.is_enabled, m.created_at, m.updated_at
        FROM menus m
        INNER JOIN role_menus rm ON m.id = rm.menu_id
        WHERE rm.role_id = @RoleId AND m.is_enabled = true
        ORDER BY m.sort_order
        """;

    #endregion

    #region Operation Log

    public const string CreateOperationLog = """
        INSERT INTO operation_logs (id, operator_id, operator_name, module, action, description, request_method, request_path, request_body, response_body, client_ip, user_agent, status_code, execution_time, created_at)
        VALUES (@Id, @OperatorId, @OperatorName, @Module, @Action, @Description, @RequestMethod, @RequestPath, @RequestBody, @ResponseBody, @ClientIp, @UserAgent, @StatusCode, @ExecutionTime, @CreatedAt)
        """;

    public const string GetOperationLogById = """
        SELECT id, operator_id, operator_name, module, action, description, request_method, request_path, request_body, response_body, client_ip, user_agent, status_code, execution_time, created_at
        FROM operation_logs WHERE id = @Id
        """;

    public const string GetOperationLogs = """
        SELECT id, operator_id, operator_name, module, action, description, request_method, request_path, client_ip, status_code, execution_time, created_at
        FROM operation_logs
        WHERE (@Module IS NULL OR module = @Module)
          AND (@Keyword IS NULL OR description ILIKE '%' || @Keyword || '%' OR operator_name ILIKE '%' || @Keyword || '%')
          AND (@StartTime IS NULL OR created_at >= @StartTime)
          AND (@EndTime IS NULL OR created_at <= @EndTime)
        ORDER BY created_at DESC
        LIMIT @Limit OFFSET @Offset
        """;

    public const string GetOperationLogCount = """
        SELECT COUNT(*)
        FROM operation_logs
        WHERE (@Module IS NULL OR module = @Module)
          AND (@Keyword IS NULL OR description ILIKE '%' || @Keyword || '%' OR operator_name ILIKE '%' || @Keyword || '%')
          AND (@StartTime IS NULL OR created_at >= @StartTime)
          AND (@EndTime IS NULL OR created_at <= @EndTime)
        """;

    public const string DeleteOperationLogs = """
        DELETE FROM operation_logs WHERE created_at < @BeforeTime
        """;

    #endregion

    #region Exception Log

    public const string CreateExceptionLog = """
        INSERT INTO exception_logs (id, module, exception_type, message, stack_trace, request_path, request_body, client_ip, status, created_at)
        VALUES (@Id, @Module, @ExceptionType, @Message, @StackTrace, @RequestPath, @RequestBody, @ClientIp, @Status, @CreatedAt)
        """;

    public const string GetExceptionLogById = """
        SELECT id, module, exception_type, message, stack_trace, request_path, request_body, client_ip, status, solution, handler_id, handled_at, created_at
        FROM exception_logs WHERE id = @Id
        """;

    public const string GetExceptionLogs = """
        SELECT id, module, exception_type, message, request_path, client_ip, status, handler_id, handled_at, created_at
        FROM exception_logs
        WHERE (@Module IS NULL OR module = @Module)
          AND (@Status IS NULL OR status = @Status)
          AND (@Keyword IS NULL OR message ILIKE '%' || @Keyword || '%' OR exception_type ILIKE '%' || @Keyword || '%')
          AND (@StartTime IS NULL OR created_at >= @StartTime)
          AND (@EndTime IS NULL OR created_at <= @EndTime)
        ORDER BY created_at DESC
        LIMIT @Limit OFFSET @Offset
        """;

    public const string GetExceptionLogCount = """
        SELECT COUNT(*)
        FROM exception_logs
        WHERE (@Module IS NULL OR module = @Module)
          AND (@Status IS NULL OR status = @Status)
          AND (@Keyword IS NULL OR message ILIKE '%' || @Keyword || '%' OR exception_type ILIKE '%' || @Keyword || '%')
          AND (@StartTime IS NULL OR created_at >= @StartTime)
          AND (@EndTime IS NULL OR created_at <= @EndTime)
        """;

    public const string UpdateExceptionLogStatus = """
        UPDATE exception_logs
        SET status = @Status, solution = @Solution, handler_id = @HandlerId, handled_at = @HandledAt
        WHERE id = @Id
        """;

    #endregion

    #region Login Log

    public const string CreateLoginLog = """
        INSERT INTO login_logs (id, user_id, username, login_type, client_ip, location, user_agent, device_type, browser, os, is_success, fail_reason, is_abnormal, abnormal_reason, created_at)
        VALUES (@Id, @UserId, @Username, @LoginType, @ClientIp, @Location, @UserAgent, @DeviceType, @Browser, @Os, @IsSuccess, @FailReason, @IsAbnormal, @AbnormalReason, @CreatedAt)
        """;

    public const string GetLoginLogById = """
        SELECT id, user_id, username, login_type, client_ip, location, user_agent, device_type, browser, os, is_success, fail_reason, is_abnormal, abnormal_reason, created_at
        FROM login_logs WHERE id = @Id
        """;

    public const string GetLoginLogs = """
        SELECT id, user_id, username, login_type, client_ip, location, device_type, browser, os, is_success, fail_reason, is_abnormal, abnormal_reason, created_at
        FROM login_logs
        WHERE (@LoginType IS NULL OR login_type = @LoginType)
          AND (@IsSuccess IS NULL OR is_success = @IsSuccess)
          AND (@IsAbnormal IS NULL OR is_abnormal = @IsAbnormal)
          AND (@Keyword IS NULL OR username ILIKE '%' || @Keyword || '%' OR client_ip ILIKE '%' || @Keyword || '%')
          AND (@StartTime IS NULL OR created_at >= @StartTime)
          AND (@EndTime IS NULL OR created_at <= @EndTime)
        ORDER BY created_at DESC
        LIMIT @Limit OFFSET @Offset
        """;

    public const string GetLoginLogCount = """
        SELECT COUNT(*)
        FROM login_logs
        WHERE (@LoginType IS NULL OR login_type = @LoginType)
          AND (@IsSuccess IS NULL OR is_success = @IsSuccess)
          AND (@IsAbnormal IS NULL OR is_abnormal = @IsAbnormal)
          AND (@Keyword IS NULL OR username ILIKE '%' || @Keyword || '%' OR client_ip ILIKE '%' || @Keyword || '%')
          AND (@StartTime IS NULL OR created_at >= @StartTime)
          AND (@EndTime IS NULL OR created_at <= @EndTime)
        """;

    public const string GetRecentLoginsByUserId = """
        SELECT id, user_id, username, login_type, client_ip, location, device_type, browser, os, is_success, fail_reason, is_abnormal, abnormal_reason, created_at
        FROM login_logs
        WHERE user_id = @UserId
        ORDER BY created_at DESC
        LIMIT @Limit
        """;

    #endregion

    #region Todo Notification

    public const string CreateNotification = """
        INSERT INTO todo_notifications (id, title, content, type, level, source_module, source_id, action_url, is_read, created_at)
        VALUES (@Id, @Title, @Content, @Type, @Level, @SourceModule, @SourceId, @ActionUrl, false, @CreatedAt)
        """;

    public const string GetNotificationById = """
        SELECT id, title, content, type, level, source_module, source_id, action_url, is_read, read_by, read_at, created_at
        FROM todo_notifications WHERE id = @Id
        """;

    public const string GetUnreadNotifications = """
        SELECT id, title, content, type, level, source_module, source_id, action_url, is_read, created_at
        FROM todo_notifications
        WHERE is_read = false
        ORDER BY level DESC, created_at DESC
        LIMIT @Limit
        """;

    public const string GetNotifications = """
        SELECT id, title, content, type, level, source_module, source_id, action_url, is_read, read_by, read_at, created_at
        FROM todo_notifications
        WHERE (@Type IS NULL OR type = @Type)
          AND (@Level IS NULL OR level = @Level)
          AND (@IsRead IS NULL OR is_read = @IsRead)
        ORDER BY created_at DESC
        LIMIT @Limit OFFSET @Offset
        """;

    public const string GetNotificationCount = """
        SELECT COUNT(*)
        FROM todo_notifications
        WHERE (@Type IS NULL OR type = @Type)
          AND (@Level IS NULL OR level = @Level)
          AND (@IsRead IS NULL OR is_read = @IsRead)
        """;

    public const string GetUnreadCount = """
        SELECT COUNT(*) FROM todo_notifications WHERE is_read = false
        """;

    public const string MarkNotificationAsRead = """
        UPDATE todo_notifications
        SET is_read = true, read_by = @ReadBy, read_at = @ReadAt
        WHERE id = @Id
        """;

    public const string MarkAllNotificationsAsRead = """
        UPDATE todo_notifications
        SET is_read = true, read_by = @ReadBy, read_at = @ReadAt
        WHERE is_read = false
        """;

    #endregion

    #region Statistics

    public const string GetArticleStats = """
        SELECT 
            COUNT(*) AS total_count,
            COUNT(*) FILTER (WHERE status = 2) AS published_count,
            COUNT(*) FILTER (WHERE status = 1) AS draft_count,
            COALESCE(SUM(view_count), 0) AS total_views
        FROM articles WHERE deleted_at IS NULL
        """;

    public const string GetProjectStats = """
        SELECT 
            COUNT(*) AS total_count,
            COUNT(*) FILTER (WHERE status = 2) AS published_count,
            COUNT(*) FILTER (WHERE status = 1) AS draft_count,
            COALESCE(SUM(view_count), 0) AS total_views
        FROM projects WHERE deleted_at IS NULL
        """;

    public const string GetCourseStats = """
        SELECT 
            COUNT(*) AS total_count,
            COUNT(*) FILTER (WHERE status = 2) AS published_count,
            COUNT(*) FILTER (WHERE status = 1) AS draft_count,
            COALESCE(SUM(play_count), 0) AS total_plays
        FROM courses WHERE deleted_at IS NULL
        """;

    public const string GetFileStats = """
        SELECT 
            COUNT(*) AS total_count,
            COALESCE(SUM(file_size), 0) AS total_size,
            COUNT(*) FILTER (WHERE status = 1) AS active_count
        FROM files WHERE deleted_at IS NULL
        """;

    public const string GetVisitorStatsToday = """
        SELECT 
            COALESCE(SUM(pv_count), 0) AS pv_count,
            COALESCE(SUM(uv_count), 0) AS uv_count
        FROM visitor_stats
        WHERE stat_date = @Date
        """;

    public const string GetVisitorStatsTrend = """
        SELECT stat_date, pv_count, uv_count
        FROM visitor_stats
        WHERE stat_date >= @StartDate AND stat_date <= @EndDate
        ORDER BY stat_date
        """;

    #endregion
}
