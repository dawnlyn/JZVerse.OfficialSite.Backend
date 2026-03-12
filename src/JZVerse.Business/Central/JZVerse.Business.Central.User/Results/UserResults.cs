using JZVerse.Business.Central.User.Database;

namespace JZVerse.Business.Central.User.Results;

/// <summary>
/// 管理员登录结果
/// </summary>
public sealed record ResultAdminLogin
{
    public string AccessToken { get; init; } = "";
    public string RefreshToken { get; init; } = "";
    public string TokenType { get; init; } = "Bearer";
    public int ExpiresIn { get; init; }
    public ResultAdminInfo? Admin { get; init; }
}

/// <summary>
/// 管理员信息结果
/// </summary>
public sealed record ResultAdminInfo
{
    public Guid Id { get; init; }
    public string Username { get; init; } = "";
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Avatar { get; init; }
    public AdminType Type { get; init; }
    public AdminStatus Status { get; init; }
    public DateTime? LastLoginAt { get; init; }
    public string? LastLoginIp { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// 访客统计结果
/// </summary>
public sealed record ResultVisitorStats
{
    public DateTime StatDate { get; init; }
    public int UniqueVisitors { get; init; }
    public int PageViews { get; init; }
    public int DirectVisits { get; init; }
    public int ReferralVisits { get; init; }
    public int PcVisits { get; init; }
    public int TabletVisits { get; init; }
    public int MobileVisits { get; init; }
}

/// <summary>
/// 访客统计汇总结果
/// </summary>
public sealed record ResultVisitorStatsSummary
{
    public long TotalUniqueVisitors { get; init; }
    public long TotalPageViews { get; init; }
    public long TotalDirectVisits { get; init; }
    public long TotalReferralVisits { get; init; }
    public long TotalPcVisits { get; init; }
    public long TotalTabletVisits { get; init; }
    public long TotalMobileVisits { get; init; }
    public IReadOnlyList<ResultVisitorStats> DailyStats { get; init; } = [];
}

/// <summary>
/// 访客行为日志结果
/// </summary>
public sealed record ResultVisitorLog
{
    public Guid Id { get; init; }
    public string SessionId { get; init; } = "";
    public string? VisitorIp { get; init; }
    public DeviceType DeviceType { get; init; }
    public string? BrowserType { get; init; }
    public string? Referer { get; init; }
    public string Module { get; init; } = "";
    public string PagePath { get; init; } = "";
    public string? PageTitle { get; init; }
    public DateTime EntryTime { get; init; }
    public int StayDuration { get; init; }
    public string? ExitPage { get; init; }
}

/// <summary>
/// 热门内容结果
/// </summary>
public sealed record ResultHotContent
{
    public string ContentType { get; init; } = "";
    public Guid ContentId { get; init; }
    public int ViewCount { get; init; }
}

/// <summary>
/// 数据看板结果
/// </summary>
public sealed record ResultDashboard
{
    /// <summary>
    /// 今日 UV
    /// </summary>
    public int TodayUv { get; init; }

    /// <summary>
    /// 今日 PV
    /// </summary>
    public int TodayPv { get; init; }

    /// <summary>
    /// 昨日 UV
    /// </summary>
    public int YesterdayUv { get; init; }

    /// <summary>
    /// 昨日 PV
    /// </summary>
    public int YesterdayPv { get; init; }

    /// <summary>
    /// 本周 UV
    /// </summary>
    public long WeekUv { get; init; }

    /// <summary>
    /// 本周 PV
    /// </summary>
    public long WeekPv { get; init; }

    /// <summary>
    /// 本月 UV
    /// </summary>
    public long MonthUv { get; init; }

    /// <summary>
    /// 本月 PV
    /// </summary>
    public long MonthPv { get; init; }

    /// <summary>
    /// 最近7天趋势
    /// </summary>
    public IReadOnlyList<ResultVisitorStats> RecentTrend { get; init; } = [];
}
