namespace JZVerse.Business.Central.User.Database;

/// <summary>
/// 管理员实体
/// </summary>
public sealed record AdminEntity
{
    /// <summary>
    /// 管理员唯一标识
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
    public string? Email { get; init; }

    /// <summary>
    /// 手机号码
    /// </summary>
    public string? Phone { get; init; }

    /// <summary>
    /// 头像 URL
    /// </summary>
    public string? Avatar { get; init; }

    /// <summary>
    /// 管理员类型
    /// </summary>
    public AdminType Type { get; init; }

    /// <summary>
    /// 账号状态
    /// </summary>
    public AdminStatus Status { get; init; }

    /// <summary>
    /// 最后登录时间
    /// </summary>
    public DateTime? LastLoginAt { get; init; }

    /// <summary>
    /// 最后登录 IP 地址
    /// </summary>
    public string? LastLoginIp { get; init; }

    /// <summary>
    /// 最后登录设备
    /// </summary>
    public string? LastLoginDevice { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime? UpdatedAt { get; init; }

    /// <summary>
    /// 软删除时间
    /// </summary>
    public DateTime? DeletedAt { get; init; }
}

/// <summary>
/// 管理员类型
/// </summary>
public enum AdminType
{
    /// <summary>
    /// 超级管理员
    /// </summary>
    SuperAdmin = 1,

    /// <summary>
    /// 普通管理员（预留）
    /// </summary>
    Admin = 2,

    /// <summary>
    /// 运营管理员（预留）
    /// </summary>
    Operator = 3
}

/// <summary>
/// 管理员状态
/// </summary>
public enum AdminStatus
{
    /// <summary>
    /// 正常
    /// </summary>
    Active = 1,

    /// <summary>
    /// 冻结
    /// </summary>
    Frozen = 2
}

/// <summary>
/// 访客统计实体
/// </summary>
public sealed record VisitorStatEntity
{
    /// <summary>
    /// 统计记录唯一标识
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 统计日期
    /// </summary>
    public DateTime StatDate { get; init; }

    /// <summary>
    /// 独立访客数
    /// </summary>
    public int UniqueVisitors { get; init; }

    /// <summary>
    /// 页面浏览量
    /// </summary>
    public int PageViews { get; init; }

    /// <summary>
    /// 直接访问次数
    /// </summary>
    public int DirectVisits { get; init; }

    /// <summary>
    /// 外部引荐访问次数
    /// </summary>
    public int ReferralVisits { get; init; }

    /// <summary>
    /// PC 端访问次数
    /// </summary>
    public int PcVisits { get; init; }

    /// <summary>
    /// 平板端访问次数
    /// </summary>
    public int TabletVisits { get; init; }

    /// <summary>
    /// 移动端访问次数
    /// </summary>
    public int MobileVisits { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// 访客行为日志实体
/// </summary>
public sealed record VisitorLogEntity
{
    /// <summary>
    /// 日志唯一标识
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 会话 ID
    /// </summary>
    public string SessionId { get; init; } = "";

    /// <summary>
    /// 访客 IP 地址
    /// </summary>
    public string? VisitorIp { get; init; }

    /// <summary>
    /// 客户端 User-Agent
    /// </summary>
    public string? UserAgent { get; init; }

    /// <summary>
    /// 设备类型
    /// </summary>
    public DeviceType DeviceType { get; init; }

    /// <summary>
    /// 浏览器类型
    /// </summary>
    public string? BrowserType { get; init; }

    /// <summary>
    /// 来源页面 URL
    /// </summary>
    public string? Referer { get; init; }

    /// <summary>
    /// 访问的模块
    /// </summary>
    public string Module { get; init; } = "";

    /// <summary>
    /// 页面路径
    /// </summary>
    public string PagePath { get; init; } = "";

    /// <summary>
    /// 页面标题
    /// </summary>
    public string? PageTitle { get; init; }

    /// <summary>
    /// 进入页面时间
    /// </summary>
    public DateTime EntryTime { get; init; }

    /// <summary>
    /// 页面停留时长（秒）
    /// </summary>
    public int StayDuration { get; init; }

    /// <summary>
    /// 离开时的页面
    /// </summary>
    public string? ExitPage { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// 设备类型
/// </summary>
public enum DeviceType
{
    /// <summary>
    /// 电脑
    /// </summary>
    Pc = 1,

    /// <summary>
    /// 平板
    /// </summary>
    Tablet = 2,

    /// <summary>
    /// 手机
    /// </summary>
    Mobile = 3,

    /// <summary>
    /// 其他
    /// </summary>
    Other = 0
}

/// <summary>
/// 用户行为同步实体（记录用户对具体内容的操作）
/// </summary>
public sealed record UserBehaviorEntity
{
    /// <summary>
    /// 行为记录唯一标识
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 会话 ID
    /// </summary>
    public string SessionId { get; init; } = "";

    /// <summary>
    /// 所属模块
    /// </summary>
    public string Module { get; init; } = "";

    /// <summary>
    /// 内容类型（article/course/project 等）
    /// </summary>
    public string ContentType { get; init; } = "";

    /// <summary>
    /// 内容 ID
    /// </summary>
    public Guid ContentId { get; init; }

    /// <summary>
    /// 行为类型
    /// </summary>
    public BehaviorType BehaviorType { get; init; }

    /// <summary>
    /// 行为发生时间
    /// </summary>
    public DateTime BehaviorTime { get; init; }

    /// <summary>
    /// 额外信息（JSON 格式）
    /// </summary>
    public string? Extra { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// 行为类型
/// </summary>
public enum BehaviorType
{
    /// <summary>
    /// 查看
    /// </summary>
    View = 1,

    /// <summary>
    /// 搜索
    /// </summary>
    Search = 2,

    /// <summary>
    /// 播放
    /// </summary>
    Play = 3,

    /// <summary>
    /// 下载
    /// </summary>
    Download = 4
}
