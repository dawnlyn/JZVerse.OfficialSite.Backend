using JZVerse.Business.Dashboard.Database;

namespace JZVerse.Business.Dashboard.Results;

#region Dashboard Overview

/// <summary>
/// 仪表盘概览数据
/// </summary>
public sealed class ResultDashboardOverview
{
    /// <summary>
    /// 今日访问统计
    /// </summary>
    public ResultVisitorStats TodayStats { get; set; } = new();

    /// <summary>
    /// 内容统计
    /// </summary>
    public ResultContentStats ContentStats { get; set; } = new();

    /// <summary>
    /// 存储统计
    /// </summary>
    public ResultStorageStats StorageStats { get; set; } = new();

    /// <summary>
    /// 服务状态
    /// </summary>
    public IReadOnlyList<ResultServiceStatus> ServiceStatuses { get; set; } = [];

    /// <summary>
    /// 待办提醒
    /// </summary>
    public IReadOnlyList<ResultTodoNotification> Notifications { get; set; } = [];

    /// <summary>
    /// 访问趋势
    /// </summary>
    public IReadOnlyList<ResultVisitorTrend> VisitorTrends { get; set; } = [];
}

/// <summary>
/// 访客统计
/// </summary>
public sealed class ResultVisitorStats
{
    public long PvCount { get; set; }
    public long UvCount { get; set; }
    public double PvGrowthRate { get; set; }
    public double UvGrowthRate { get; set; }
}

/// <summary>
/// 内容统计
/// </summary>
public sealed class ResultContentStats
{
    public ResultModuleStats Articles { get; set; } = new();
    public ResultModuleStats Projects { get; set; } = new();
    public ResultModuleStats Courses { get; set; } = new();
}

/// <summary>
/// 模块统计
/// </summary>
public sealed class ResultModuleStats
{
    public int TotalCount { get; set; }
    public int PublishedCount { get; set; }
    public int DraftCount { get; set; }
    public long TotalViews { get; set; }
}

/// <summary>
/// 存储统计
/// </summary>
public sealed class ResultStorageStats
{
    public int TotalFiles { get; set; }
    public long TotalSize { get; set; }
    public long UsedSize { get; set; }
    public double UsagePercent { get; set; }
    public string FormattedTotalSize { get; set; } = "";
    public string FormattedUsedSize { get; set; } = "";
}

/// <summary>
/// 服务状态
/// </summary>
public sealed class ResultServiceStatus
{
    public string ServiceName { get; set; } = "";
    public string ServiceCode { get; set; } = "";
    public ServiceHealthStatus Status { get; set; }
    public string? Message { get; set; }
    public DateTime? LastCheckTime { get; set; }
}

/// <summary>
/// 服务健康状态
/// </summary>
public enum ServiceHealthStatus
{
    Healthy = 1,
    Degraded = 2,
    Unhealthy = 3,
    Unknown = 4
}

/// <summary>
/// 访问趋势
/// </summary>
public sealed class ResultVisitorTrend
{
    public DateTime Date { get; set; }
    public long PvCount { get; set; }
    public long UvCount { get; set; }
}

#endregion

#region Config Results

/// <summary>
/// 系统配置信息
/// </summary>
public sealed class ResultConfigInfo
{
    public Guid Id { get; set; }
    public string ConfigKey { get; set; } = "";
    public string ConfigValue { get; set; } = "";
    public string? Description { get; set; }
    public ConfigCategory Category { get; set; }
    public bool IsPublic { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// 分组配置
/// </summary>
public sealed class ResultConfigGroup
{
    public ConfigCategory Category { get; set; }
    public string CategoryName { get; set; } = "";
    public IReadOnlyList<ResultConfigInfo> Configs { get; set; } = [];
}

#endregion

#region Menu Results

/// <summary>
/// 菜单信息
/// </summary>
public sealed class ResultMenuInfo
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = "";
    public string? Icon { get; set; }
    public string? Path { get; set; }
    public string? Component { get; set; }
    public string? Permission { get; set; }
    public int SortOrder { get; set; }
    public bool IsVisible { get; set; }
    public bool IsEnabled { get; set; }
    public IReadOnlyList<ResultMenuInfo> Children { get; set; } = [];
}

#endregion

#region Role Results

/// <summary>
/// 角色信息
/// </summary>
public sealed class ResultRoleInfo
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public IReadOnlyList<Guid> MenuIds { get; set; } = [];
}

#endregion

#region Log Results

/// <summary>
/// 操作日志信息
/// </summary>
public sealed class ResultOperationLog
{
    public Guid Id { get; set; }
    public Guid? OperatorId { get; set; }
    public string? OperatorName { get; set; }
    public string Module { get; set; } = "";
    public string Action { get; set; } = "";
    public string? Description { get; set; }
    public string? RequestMethod { get; set; }
    public string? RequestPath { get; set; }
    public string? RequestBody { get; set; }
    public string? ResponseBody { get; set; }
    public string? ClientIp { get; set; }
    public int StatusCode { get; set; }
    public long ExecutionTime { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 异常日志信息
/// </summary>
public sealed class ResultExceptionLog
{
    public Guid Id { get; set; }
    public string Module { get; set; } = "";
    public string ExceptionType { get; set; } = "";
    public string Message { get; set; } = "";
    public string? StackTrace { get; set; }
    public string? RequestPath { get; set; }
    public string? RequestBody { get; set; }
    public string? ClientIp { get; set; }
    public ExceptionLogStatus Status { get; set; }
    public string? Solution { get; set; }
    public Guid? HandlerId { get; set; }
    public DateTime? HandledAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 登录日志信息
/// </summary>
public sealed class ResultLoginLog
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string? Username { get; set; }
    public LoginLogType LoginType { get; set; }
    public string? ClientIp { get; set; }
    public string? Location { get; set; }
    public string? DeviceType { get; set; }
    public string? Browser { get; set; }
    public string? Os { get; set; }
    public bool IsSuccess { get; set; }
    public string? FailReason { get; set; }
    public bool IsAbnormal { get; set; }
    public string? AbnormalReason { get; set; }
    public DateTime CreatedAt { get; set; }
}

#endregion

#region Notification Results

/// <summary>
/// 待办通知信息
/// </summary>
public sealed class ResultTodoNotification
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string? Content { get; set; }
    public TodoNotificationType Type { get; set; }
    public TodoNotificationLevel Level { get; set; }
    public string? SourceModule { get; set; }
    public string? SourceId { get; set; }
    public string? ActionUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 未读通知统计
/// </summary>
public sealed class ResultUnreadStats
{
    public int TotalUnread { get; set; }
    public int SecurityAlerts { get; set; }
    public int StorageAlerts { get; set; }
    public int LoginAlerts { get; set; }
    public int OtherAlerts { get; set; }
}

#endregion

#region Statistics Results

/// <summary>
/// 访问统计详情
/// </summary>
public sealed class ResultVisitStatistics
{
    public long TotalPv { get; set; }
    public long TotalUv { get; set; }
    public IReadOnlyList<ResultVisitorTrend> Trends { get; set; } = [];
    public IReadOnlyList<ResultModuleVisit> ModuleVisits { get; set; } = [];
}

/// <summary>
/// 模块访问统计
/// </summary>
public sealed class ResultModuleVisit
{
    public string Module { get; set; } = "";
    public string ModuleName { get; set; } = "";
    public long PvCount { get; set; }
    public long UvCount { get; set; }
    public double Percentage { get; set; }
}

/// <summary>
/// 内容统计详情
/// </summary>
public sealed class ResultContentStatistics
{
    public ResultModuleStats Articles { get; set; } = new();
    public ResultModuleStats Projects { get; set; } = new();
    public ResultModuleStats Courses { get; set; } = new();
    public IReadOnlyList<ResultHotContent> HotContents { get; set; } = [];
}

/// <summary>
/// 热门内容
/// </summary>
public sealed class ResultHotContent
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Module { get; set; } = "";
    public long ViewCount { get; set; }
    public DateTime PublishedAt { get; set; }
}

#endregion
