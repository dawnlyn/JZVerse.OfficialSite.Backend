using System.ComponentModel.DataAnnotations;
using JZVerse.Business.Dashboard.Database;

namespace JZVerse.Business.Dashboard.Arguments;

#region Config Arguments

/// <summary>
/// 保存系统配置
/// </summary>
public sealed class ArgSaveConfig
{
    [Required(ErrorMessage = "配置键不能为空")]
    [StringLength(100, ErrorMessage = "配置键长度不能超过100个字符")]
    public string ConfigKey { get; set; } = "";

    [Required(ErrorMessage = "配置值不能为空")]
    public string ConfigValue { get; set; } = "";

    [StringLength(500, ErrorMessage = "描述长度不能超过500个字符")]
    public string? Description { get; set; }

    public ConfigCategory Category { get; set; }

    public bool IsPublic { get; set; }
}

/// <summary>
/// 批量保存配置
/// </summary>
public sealed class ArgSaveConfigBatch
{
    [Required]
    [MinLength(1, ErrorMessage = "至少需要一个配置项")]
    public IReadOnlyList<ArgSaveConfig> Configs { get; set; } = [];
}

#endregion

#region Menu Arguments

/// <summary>
/// 保存菜单
/// </summary>
public sealed class ArgSaveMenu
{
    public Guid? Id { get; set; }

    public Guid? ParentId { get; set; }

    [Required(ErrorMessage = "菜单名称不能为空")]
    [StringLength(50, ErrorMessage = "菜单名称长度不能超过50个字符")]
    public string Name { get; set; } = "";

    [StringLength(50, ErrorMessage = "图标长度不能超过50个字符")]
    public string? Icon { get; set; }

    [StringLength(200, ErrorMessage = "路径长度不能超过200个字符")]
    public string? Path { get; set; }

    [StringLength(200, ErrorMessage = "组件长度不能超过200个字符")]
    public string? Component { get; set; }

    [StringLength(100, ErrorMessage = "权限标识长度不能超过100个字符")]
    public string? Permission { get; set; }

    public int SortOrder { get; set; }

    public bool IsVisible { get; set; } = true;

    public bool IsEnabled { get; set; } = true;
}

#endregion

#region Role Arguments

/// <summary>
/// 保存角色
/// </summary>
public sealed class ArgSaveRole
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "角色编码不能为空")]
    [StringLength(50, ErrorMessage = "角色编码长度不能超过50个字符")]
    public string Code { get; set; } = "";

    [Required(ErrorMessage = "角色名称不能为空")]
    [StringLength(50, ErrorMessage = "角色名称长度不能超过50个字符")]
    public string Name { get; set; } = "";

    [StringLength(500, ErrorMessage = "描述长度不能超过500个字符")]
    public string? Description { get; set; }

    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// 分配菜单给角色
/// </summary>
public sealed class ArgAssignMenusToRole
{
    [Required]
    public Guid RoleId { get; set; }

    [Required]
    public IReadOnlyList<Guid> MenuIds { get; set; } = [];
}

#endregion

#region Log Query Arguments

/// <summary>
/// 查询操作日志
/// </summary>
public sealed class ArgQueryOperationLogs
{
    public string? Module { get; set; }

    public string? Keyword { get; set; }

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    [Range(1, int.MaxValue)]
    public int PageIndex { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// 查询异常日志
/// </summary>
public sealed class ArgQueryExceptionLogs
{
    public string? Module { get; set; }

    public ExceptionLogStatus? Status { get; set; }

    public string? Keyword { get; set; }

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    [Range(1, int.MaxValue)]
    public int PageIndex { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// 处理异常日志
/// </summary>
public sealed class ArgHandleExceptionLog
{
    [Required]
    public Guid Id { get; set; }

    public ExceptionLogStatus Status { get; set; }

    [StringLength(2000, ErrorMessage = "解决方案长度不能超过2000个字符")]
    public string? Solution { get; set; }
}

/// <summary>
/// 查询登录日志
/// </summary>
public sealed class ArgQueryLoginLogs
{
    public LoginLogType? LoginType { get; set; }

    public bool? IsSuccess { get; set; }

    public bool? IsAbnormal { get; set; }

    public string? Keyword { get; set; }

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    [Range(1, int.MaxValue)]
    public int PageIndex { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

#endregion

#region Notification Arguments

/// <summary>
/// 查询待办通知
/// </summary>
public sealed class ArgQueryNotifications
{
    public TodoNotificationType? Type { get; set; }

    public TodoNotificationLevel? Level { get; set; }

    public bool? IsRead { get; set; }

    [Range(1, int.MaxValue)]
    public int PageIndex { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

#endregion

#region Statistics Arguments

/// <summary>
/// 统计时间范围
/// </summary>
public sealed class ArgStatisticsRange
{
    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    /// <summary>
    /// 统计粒度: day, week, month
    /// </summary>
    public string Granularity { get; set; } = "day";
}

#endregion
