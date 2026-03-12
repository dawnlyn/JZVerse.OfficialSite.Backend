using JZVerse.Business.Abstractions.Models;
using JZVerse.Business.Central.User.Arguments;
using JZVerse.Business.Central.User.Database;
using JZVerse.Business.Central.User.Results;
using JZVerse.DataAccess.Abstractions;

namespace JZVerse.Business.Central.User.Services;

/// <summary>
/// 访客统计服务
/// </summary>
public sealed class VisitorStatsService
{
    private readonly IDbExecutor _db;
    private readonly ISnowflakeIdGenerator _idGenerator;

    public VisitorStatsService(IDbExecutor db, ISnowflakeIdGenerator idGenerator)
    {
        _db = db;
        _idGenerator = idGenerator;
    }

    /// <summary>
    /// 记录访客行为日志
    /// </summary>
    public async Task<Guid> RecordVisitorLogAsync(ArgRecordVisitorLog arg)
    {
        var deviceType = ParseDeviceType(arg.UserAgent);
        var browserType = ParseBrowserType(arg.UserAgent);

        var log = new VisitorLogEntity
        {
            Id = _idGenerator.GenerateGuid(),
            SessionId = arg.SessionId,
            VisitorIp = arg.VisitorIp,
            UserAgent = arg.UserAgent,
            DeviceType = deviceType,
            BrowserType = browserType,
            Referer = arg.Referer,
            Module = arg.Module,
            PagePath = arg.PagePath,
            PageTitle = arg.PageTitle,
            EntryTime = DateTime.UtcNow,
            StayDuration = 0,
            CreatedAt = DateTime.UtcNow
        };

        await _db.ExecuteAsync(UserSql.CreateVisitorLog, log);

        // 更新当日统计
        await UpdateDailyStatsAsync(deviceType, !string.IsNullOrEmpty(arg.Referer));

        return log.Id;
    }

    /// <summary>
    /// 更新访客退出信息
    /// </summary>
    public async Task UpdateVisitorExitAsync(ArgUpdateVisitorExit arg)
    {
        await _db.ExecuteAsync(UserSql.UpdateVisitorLogExit, new
        {
            Id = arg.LogId,
            arg.StayDuration,
            arg.ExitPage
        });
    }

    /// <summary>
    /// 记录用户行为
    /// </summary>
    public async Task RecordUserBehaviorAsync(ArgRecordUserBehavior arg)
    {
        var behavior = new UserBehaviorEntity
        {
            Id = _idGenerator.GenerateGuid(),
            SessionId = arg.SessionId,
            Module = arg.Module,
            ContentType = arg.ContentType,
            ContentId = arg.ContentId,
            BehaviorType = (BehaviorType)arg.BehaviorType,
            BehaviorTime = DateTime.UtcNow,
            Extra = arg.Extra,
            CreatedAt = DateTime.UtcNow
        };

        await _db.ExecuteAsync(UserSql.CreateUserBehavior, behavior);
    }

    /// <summary>
    /// 获取访客统计
    /// </summary>
    public async Task<ResultVisitorStatsSummary> GetStatsAsync(ArgQueryVisitorStats arg)
    {
        var startDate = arg.StartDate ?? DateTime.UtcNow.Date.AddDays(-30);
        var endDate = arg.EndDate ?? DateTime.UtcNow.Date;

        var stats = await _db.QueryAsync<VisitorStatEntity>(
            UserSql.GetVisitorStatsByDateRange,
            new { StartDate = startDate, EndDate = endDate });

        var dailyStats = stats.Select(s => new ResultVisitorStats
        {
            StatDate = s.StatDate,
            UniqueVisitors = s.UniqueVisitors,
            PageViews = s.PageViews,
            DirectVisits = s.DirectVisits,
            ReferralVisits = s.ReferralVisits,
            PcVisits = s.PcVisits,
            TabletVisits = s.TabletVisits,
            MobileVisits = s.MobileVisits
        }).ToList();

        return new ResultVisitorStatsSummary
        {
            TotalUniqueVisitors = dailyStats.Sum(s => s.UniqueVisitors),
            TotalPageViews = dailyStats.Sum(s => s.PageViews),
            TotalDirectVisits = dailyStats.Sum(s => s.DirectVisits),
            TotalReferralVisits = dailyStats.Sum(s => s.ReferralVisits),
            TotalPcVisits = dailyStats.Sum(s => s.PcVisits),
            TotalTabletVisits = dailyStats.Sum(s => s.TabletVisits),
            TotalMobileVisits = dailyStats.Sum(s => s.MobileVisits),
            DailyStats = dailyStats
        };
    }

    /// <summary>
    /// 获取访客行为日志列表
    /// </summary>
    public async Task<PagedResult<ResultVisitorLog>> GetVisitorLogsAsync(ArgQueryVisitorLogs arg)
    {
        var offset = (arg.PageIndex - 1) * arg.PageSize;

        var logs = await _db.QueryAsync<VisitorLogEntity>(
            UserSql.GetVisitorLogList,
            new
            {
                arg.Module,
                arg.DeviceType,
                arg.StartTime,
                arg.EndTime,
                Limit = arg.PageSize,
                Offset = offset
            });

        var total = await _db.ExecuteScalarAsync<int>(
            UserSql.GetVisitorLogCount,
            new { arg.Module, arg.DeviceType, arg.StartTime, arg.EndTime });

        return new PagedResult<ResultVisitorLog>
        {
            Items = logs.Select(MapToVisitorLog).ToList(),
            TotalCount = total,
            PageIndex = arg.PageIndex,
            PageSize = arg.PageSize
        };
    }

    /// <summary>
    /// 获取热门内容
    /// </summary>
    public async Task<IReadOnlyList<ResultHotContent>> GetHotContentAsync(string module, int limit = 10)
    {
        var startTime = DateTime.UtcNow.AddDays(-7);

        var results = await _db.QueryAsync<dynamic>(
            UserSql.GetHotContent,
            new { Module = module, StartTime = startTime, Limit = limit });

        return results.Select(r => new ResultHotContent
        {
            ContentType = (string)r.content_type,
            ContentId = Guid.Parse((string)r.content_id),
            ViewCount = (int)r.view_count
        }).ToList();
    }

    /// <summary>
    /// 获取数据看板
    /// </summary>
    public async Task<ResultDashboard> GetDashboardAsync()
    {
        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var last7Days = today.AddDays(-6);

        // 获取今日统计
        var todayStats = await _db.QueryAsync<VisitorStatEntity>(
            UserSql.GetVisitorStatsByDateRange,
            new { StartDate = today, EndDate = today });
        var todayStat = todayStats.FirstOrDefault();

        // 获取昨日统计
        var yesterdayStats = await _db.QueryAsync<VisitorStatEntity>(
            UserSql.GetVisitorStatsByDateRange,
            new { StartDate = yesterday, EndDate = yesterday });
        var yesterdayStat = yesterdayStats.FirstOrDefault();

        // 获取本周统计
        var weekStats = await _db.QueryAsync<VisitorStatEntity>(
            UserSql.GetVisitorStatsByDateRange,
            new { StartDate = weekStart, EndDate = today });

        // 获取本月统计
        var monthStats = await _db.QueryAsync<VisitorStatEntity>(
            UserSql.GetVisitorStatsByDateRange,
            new { StartDate = monthStart, EndDate = today });

        // 获取最近7天趋势
        var trendStats = await _db.QueryAsync<VisitorStatEntity>(
            UserSql.GetVisitorStatsByDateRange,
            new { StartDate = last7Days, EndDate = today });

        return new ResultDashboard
        {
            TodayUv = todayStat?.UniqueVisitors ?? 0,
            TodayPv = todayStat?.PageViews ?? 0,
            YesterdayUv = yesterdayStat?.UniqueVisitors ?? 0,
            YesterdayPv = yesterdayStat?.PageViews ?? 0,
            WeekUv = weekStats.Sum(s => s.UniqueVisitors),
            WeekPv = weekStats.Sum(s => s.PageViews),
            MonthUv = monthStats.Sum(s => s.UniqueVisitors),
            MonthPv = monthStats.Sum(s => s.PageViews),
            RecentTrend = trendStats.Select(s => new ResultVisitorStats
            {
                StatDate = s.StatDate,
                UniqueVisitors = s.UniqueVisitors,
                PageViews = s.PageViews,
                DirectVisits = s.DirectVisits,
                ReferralVisits = s.ReferralVisits,
                PcVisits = s.PcVisits,
                TabletVisits = s.TabletVisits,
                MobileVisits = s.MobileVisits
            }).OrderBy(s => s.StatDate).ToList()
        };
    }

    private async Task UpdateDailyStatsAsync(DeviceType deviceType, bool isReferral)
    {
        var today = DateTime.UtcNow.Date;

        await _db.ExecuteAsync(UserSql.UpsertVisitorStat, new
        {
            Id = _idGenerator.GenerateGuid(),
            StatDate = today,
            UniqueVisitors = 1,
            PageViews = 1,
            DirectVisits = isReferral ? 0 : 1,
            ReferralVisits = isReferral ? 1 : 0,
            PcVisits = deviceType == DeviceType.Pc ? 1 : 0,
            TabletVisits = deviceType == DeviceType.Tablet ? 1 : 0,
            MobileVisits = deviceType == DeviceType.Mobile ? 1 : 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
    }

    private static DeviceType ParseDeviceType(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
            return DeviceType.Other;

        userAgent = userAgent.ToLower();

        if (userAgent.Contains("mobile") || userAgent.Contains("android") && !userAgent.Contains("tablet"))
            return DeviceType.Mobile;

        if (userAgent.Contains("tablet") || userAgent.Contains("ipad"))
            return DeviceType.Tablet;

        if (userAgent.Contains("windows") || userAgent.Contains("macintosh") || userAgent.Contains("linux"))
            return DeviceType.Pc;

        return DeviceType.Other;
    }

    private static string? ParseBrowserType(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
            return null;

        userAgent = userAgent.ToLower();

        if (userAgent.Contains("edg"))
            return "Edge";
        if (userAgent.Contains("chrome"))
            return "Chrome";
        if (userAgent.Contains("firefox"))
            return "Firefox";
        if (userAgent.Contains("safari"))
            return "Safari";
        if (userAgent.Contains("opera") || userAgent.Contains("opr"))
            return "Opera";

        return "Other";
    }

    private static ResultVisitorLog MapToVisitorLog(VisitorLogEntity entity) => new()
    {
        Id = entity.Id,
        SessionId = entity.SessionId,
        VisitorIp = entity.VisitorIp,
        DeviceType = entity.DeviceType,
        BrowserType = entity.BrowserType,
        Referer = entity.Referer,
        Module = entity.Module,
        PagePath = entity.PagePath,
        PageTitle = entity.PageTitle,
        EntryTime = entity.EntryTime,
        StayDuration = entity.StayDuration,
        ExitPage = entity.ExitPage
    };
}
