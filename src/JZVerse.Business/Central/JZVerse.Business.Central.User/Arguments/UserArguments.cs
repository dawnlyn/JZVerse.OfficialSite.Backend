using JZVerse.Business.Abstractions.Validation;

namespace JZVerse.Business.Central.User.Arguments;

/// <summary>
/// 管理员登录入参
/// </summary>
public sealed record ArgAdminLogin
{
    [Required(ErrorMessage = "用户名不能为空")]
    [StringLength(MinLength = 3, MaxLength = 50, ErrorMessage = "用户名长度必须在3-50之间")]
    public string Username { get; init; } = "";

    [Required(ErrorMessage = "密码不能为空")]
    [StringLength(MinLength = 6, MaxLength = 100, ErrorMessage = "密码长度必须在6-100之间")]
    public string Password { get; init; } = "";
}

/// <summary>
/// 创建管理员入参
/// </summary>
public sealed record ArgCreateAdmin
{
    [Required(ErrorMessage = "用户名不能为空")]
    [StringLength(MinLength = 3, MaxLength = 50, ErrorMessage = "用户名长度必须在3-50之间")]
    [Regex(@"^[a-zA-Z0-9_]+$", ErrorMessage = "用户名只能包含字母、数字和下划线")]
    public string Username { get; init; } = "";

    [Required(ErrorMessage = "密码不能为空")]
    [StringLength(MinLength = 6, MaxLength = 100, ErrorMessage = "密码长度必须在6-100之间")]
    public string Password { get; init; } = "";

    [Email(ErrorMessage = "邮箱格式不正确")]
    public string? Email { get; init; }

    [Phone(ErrorMessage = "手机号格式不正确")]
    public string? Phone { get; init; }
}

/// <summary>
/// 更新管理员密码入参
/// </summary>
public sealed record ArgUpdateAdminPassword
{
    [Required(ErrorMessage = "原密码不能为空")]
    public string OldPassword { get; init; } = "";

    [Required(ErrorMessage = "新密码不能为空")]
    [StringLength(MinLength = 6, MaxLength = 100, ErrorMessage = "新密码长度必须在6-100之间")]
    public string NewPassword { get; init; } = "";
}

/// <summary>
/// 更新管理员资料入参
/// </summary>
public sealed record ArgUpdateAdminProfile
{
    [Email(ErrorMessage = "邮箱格式不正确")]
    public string? Email { get; init; }

    [Phone(ErrorMessage = "手机号格式不正确")]
    public string? Phone { get; init; }

    public string? Avatar { get; init; }
}

/// <summary>
/// 记录访客行为入参
/// </summary>
public sealed record ArgRecordVisitorLog
{
    [Required(ErrorMessage = "会话ID不能为空")]
    public string SessionId { get; init; } = "";

    public string? VisitorIp { get; init; }

    public string? UserAgent { get; init; }

    [Required(ErrorMessage = "模块不能为空")]
    public string Module { get; init; } = "";

    [Required(ErrorMessage = "页面路径不能为空")]
    public string PagePath { get; init; } = "";

    public string? PageTitle { get; init; }

    public string? Referer { get; init; }
}

/// <summary>
/// 更新访客退出信息入参
/// </summary>
public sealed record ArgUpdateVisitorExit
{
    [Required(ErrorMessage = "日志ID不能为空")]
    public Guid LogId { get; init; }

    [Range(0, int.MaxValue, ErrorMessage = "停留时间不能为负数")]
    public int StayDuration { get; init; }

    public string? ExitPage { get; init; }
}

/// <summary>
/// 记录用户行为入参
/// </summary>
public sealed record ArgRecordUserBehavior
{
    [Required(ErrorMessage = "会话ID不能为空")]
    public string SessionId { get; init; } = "";

    [Required(ErrorMessage = "模块不能为空")]
    public string Module { get; init; } = "";

    [Required(ErrorMessage = "内容类型不能为空")]
    public string ContentType { get; init; } = "";

    public Guid ContentId { get; init; }

    [Required(ErrorMessage = "行为类型不能为空")]
    public int BehaviorType { get; init; }

    public string? Extra { get; init; }
}

/// <summary>
/// 查询访客统计入参
/// </summary>
public sealed record ArgQueryVisitorStats
{
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
}

/// <summary>
/// 查询访客日志入参
/// </summary>
public sealed record ArgQueryVisitorLogs
{
    public string? Module { get; init; }
    public int? DeviceType { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime? EndTime { get; init; }

    [Range(1, 100, ErrorMessage = "每页数量必须在1-100之间")]
    public int PageSize { get; init; } = 20;

    [Range(1, int.MaxValue, ErrorMessage = "页码必须大于0")]
    public int PageIndex { get; init; } = 1;
}
