namespace JZVerse.Business.Central.User.Database;

/// <summary>
/// 用户中心 SQL 语句
/// </summary>
public static class UserSql
{
    #region 管理员相关

    /// <summary>
    /// 创建管理员
    /// </summary>
    public const string CreateAdmin = """
        INSERT INTO admins (id, username, password_hash, email, phone, avatar, type, status, created_at)
        VALUES (@Id, @Username, @PasswordHash, @Email, @Phone, @Avatar, @Type, @Status, @CreatedAt)
        """;

    /// <summary>
    /// 根据 ID 获取管理员
    /// </summary>
    public const string GetAdminById = """
        SELECT id, username, password_hash, email, phone, avatar, type, status,
               last_login_at, last_login_ip, last_login_device, created_at, updated_at, deleted_at
        FROM admins
        WHERE id = @Id AND deleted_at IS NULL
        """;

    /// <summary>
    /// 根据用户名获取管理员
    /// </summary>
    public const string GetAdminByUsername = """
        SELECT id, username, password_hash, email, phone, avatar, type, status,
               last_login_at, last_login_ip, last_login_device, created_at, updated_at, deleted_at
        FROM admins
        WHERE username = @Username AND deleted_at IS NULL
        """;

    /// <summary>
    /// 更新管理员密码
    /// </summary>
    public const string UpdateAdminPassword = """
        UPDATE admins
        SET password_hash = @PasswordHash, updated_at = @UpdatedAt
        WHERE id = @Id AND deleted_at IS NULL
        """;

    /// <summary>
    /// 更新管理员状态
    /// </summary>
    public const string UpdateAdminStatus = """
        UPDATE admins
        SET status = @Status, updated_at = @UpdatedAt
        WHERE id = @Id AND deleted_at IS NULL
        """;

    /// <summary>
    /// 更新管理员登录信息
    /// </summary>
    public const string UpdateAdminLoginInfo = """
        UPDATE admins
        SET last_login_at = @LastLoginAt, last_login_ip = @LastLoginIp,
            last_login_device = @LastLoginDevice, updated_at = @UpdatedAt
        WHERE id = @Id AND deleted_at IS NULL
        """;

    /// <summary>
    /// 更新管理员基本信息
    /// </summary>
    public const string UpdateAdminProfile = """
        UPDATE admins
        SET email = @Email, phone = @Phone, avatar = @Avatar, updated_at = @UpdatedAt
        WHERE id = @Id AND deleted_at IS NULL
        """;

    /// <summary>
    /// 软删除管理员
    /// </summary>
    public const string SoftDeleteAdmin = """
        UPDATE admins
        SET deleted_at = @DeletedAt
        WHERE id = @Id AND deleted_at IS NULL
        """;

    /// <summary>
    /// 检查用户名是否存在
    /// </summary>
    public const string CheckAdminUsernameExists = """
        SELECT EXISTS(SELECT 1 FROM admins WHERE username = @Username AND deleted_at IS NULL)
        """;

    /// <summary>
    /// 获取管理员列表
    /// </summary>
    public const string GetAdminList = """
        SELECT id, username, password_hash, email, phone, avatar, type, status,
               last_login_at, last_login_ip, last_login_device, created_at, updated_at
        FROM admins
        WHERE deleted_at IS NULL
        ORDER BY created_at DESC
        LIMIT @Limit OFFSET @Offset
        """;

    /// <summary>
    /// 获取管理员总数
    /// </summary>
    public const string GetAdminCount = """
        SELECT COUNT(*) FROM admins WHERE deleted_at IS NULL
        """;

    #endregion

    #region 访客统计相关

    /// <summary>
    /// 插入或更新访客统计
    /// </summary>
    public const string UpsertVisitorStat = """
        INSERT INTO visitor_stats (id, stat_date, unique_visitors, page_views, direct_visits, referral_visits,
                                   pc_visits, tablet_visits, mobile_visits, created_at)
        VALUES (@Id, @StatDate, @UniqueVisitors, @PageViews, @DirectVisits, @ReferralVisits,
                @PcVisits, @TabletVisits, @MobileVisits, @CreatedAt)
        ON CONFLICT (stat_date) DO UPDATE SET
            unique_visitors = visitor_stats.unique_visitors + @UniqueVisitors,
            page_views = visitor_stats.page_views + @PageViews,
            direct_visits = visitor_stats.direct_visits + @DirectVisits,
            referral_visits = visitor_stats.referral_visits + @ReferralVisits,
            pc_visits = visitor_stats.pc_visits + @PcVisits,
            tablet_visits = visitor_stats.tablet_visits + @TabletVisits,
            mobile_visits = visitor_stats.mobile_visits + @MobileVisits,
            updated_at = @UpdatedAt
        """;

    /// <summary>
    /// 获取日期范围内的访客统计
    /// </summary>
    public const string GetVisitorStatsByDateRange = """
        SELECT id, stat_date, unique_visitors, page_views, direct_visits, referral_visits,
               pc_visits, tablet_visits, mobile_visits, created_at, updated_at
        FROM visitor_stats
        WHERE stat_date BETWEEN @StartDate AND @EndDate
        ORDER BY stat_date DESC
        """;

    /// <summary>
    /// 获取访客统计汇总
    /// </summary>
    public const string GetVisitorStatsSummary = """
        SELECT SUM(unique_visitors) AS total_uv, SUM(page_views) AS total_pv,
               SUM(direct_visits) AS total_direct, SUM(referral_visits) AS total_referral,
               SUM(pc_visits) AS total_pc, SUM(tablet_visits) AS total_tablet, SUM(mobile_visits) AS total_mobile
        FROM visitor_stats
        WHERE stat_date BETWEEN @StartDate AND @EndDate
        """;

    #endregion

    #region 访客行为日志相关

    /// <summary>
    /// 创建访客行为日志
    /// </summary>
    public const string CreateVisitorLog = """
        INSERT INTO visitor_logs (id, session_id, visitor_ip, user_agent, device_type, browser_type,
                                  referer, module, page_path, page_title, entry_time, stay_duration, exit_page, created_at)
        VALUES (@Id, @SessionId, @VisitorIp, @UserAgent, @DeviceType, @BrowserType,
                @Referer, @Module, @PagePath, @PageTitle, @EntryTime, @StayDuration, @ExitPage, @CreatedAt)
        """;

    /// <summary>
    /// 更新访客停留时间和退出页面
    /// </summary>
    public const string UpdateVisitorLogExit = """
        UPDATE visitor_logs
        SET stay_duration = @StayDuration, exit_page = @ExitPage
        WHERE id = @Id
        """;

    /// <summary>
    /// 获取访客行为日志列表
    /// </summary>
    public const string GetVisitorLogList = """
        SELECT id, session_id, visitor_ip, user_agent, device_type, browser_type,
               referer, module, page_path, page_title, entry_time, stay_duration, exit_page, created_at
        FROM visitor_logs
        WHERE (@Module IS NULL OR module = @Module)
          AND (@DeviceType IS NULL OR device_type = @DeviceType)
          AND (@StartTime IS NULL OR entry_time >= @StartTime)
          AND (@EndTime IS NULL OR entry_time <= @EndTime)
        ORDER BY entry_time DESC
        LIMIT @Limit OFFSET @Offset
        """;

    /// <summary>
    /// 获取访客行为日志总数
    /// </summary>
    public const string GetVisitorLogCount = """
        SELECT COUNT(*) FROM visitor_logs
        WHERE (@Module IS NULL OR module = @Module)
          AND (@DeviceType IS NULL OR device_type = @DeviceType)
          AND (@StartTime IS NULL OR entry_time >= @StartTime)
          AND (@EndTime IS NULL OR entry_time <= @EndTime)
        """;

    #endregion

    #region 用户行为同步相关

    /// <summary>
    /// 创建用户行为记录
    /// </summary>
    public const string CreateUserBehavior = """
        INSERT INTO user_behaviors (id, session_id, module, content_type, content_id, behavior_type, behavior_time, extra, created_at)
        VALUES (@Id, @SessionId, @Module, @ContentType, @ContentId, @BehaviorType, @BehaviorTime, @Extra, @CreatedAt)
        """;

    /// <summary>
    /// 获取内容行为统计
    /// </summary>
    public const string GetContentBehaviorStats = """
        SELECT content_type, content_id, behavior_type, COUNT(*) AS count
        FROM user_behaviors
        WHERE module = @Module
          AND (@ContentType IS NULL OR content_type = @ContentType)
          AND behavior_time BETWEEN @StartTime AND @EndTime
        GROUP BY content_type, content_id, behavior_type
        ORDER BY count DESC
        LIMIT @Limit
        """;

    /// <summary>
    /// 获取热门内容
    /// </summary>
    public const string GetHotContent = """
        SELECT content_type, content_id, COUNT(*) AS view_count
        FROM user_behaviors
        WHERE module = @Module AND behavior_type = 1
          AND behavior_time >= @StartTime
        GROUP BY content_type, content_id
        ORDER BY view_count DESC
        LIMIT @Limit
        """;

    #endregion
}
