using JZVerse.Business.Dashboard.Database;
using JZVerse.Business.Dashboard.Results;
using JZVerse.DataAccess.Abstractions;

namespace JZVerse.Business.Dashboard.Services;

/// <summary>
/// 仪表盘服务 - 首页数据汇总
/// </summary>
public sealed class DashboardService
{
    private readonly IDbExecutor _db;

    public DashboardService(IDbExecutor db)
    {
        _db = db;
    }

    /// <summary>
    /// 获取仪表盘概览数据
    /// </summary>
    public async Task<ResultDashboardOverview> GetOverviewAsync()
    {
        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);

        // 并行获取各项统计数据
        var todayStatsTask = GetVisitorStatsAsync(today);
        var yesterdayStatsTask = GetVisitorStatsAsync(yesterday);
        var contentStatsTask = GetContentStatsAsync();
        var storageStatsTask = GetStorageStatsAsync();
        var notificationsTask = GetUnreadNotificationsAsync(10);
        var trendsTask = GetVisitorTrendsAsync(today.AddDays(-7), today);

        await Task.WhenAll(todayStatsTask, yesterdayStatsTask, contentStatsTask, storageStatsTask, notificationsTask, trendsTask);

        var todayStats = await todayStatsTask;
        var yesterdayStats = await yesterdayStatsTask;
        var contentStats = await contentStatsTask;
        var storageStats = await storageStatsTask;
        var notifications = await notificationsTask;
        var trends = await trendsTask;

        // 计算增长率
        todayStats.PvGrowthRate = CalculateGrowthRate(todayStats.PvCount, yesterdayStats.PvCount);
        todayStats.UvGrowthRate = CalculateGrowthRate(todayStats.UvCount, yesterdayStats.UvCount);

        return new ResultDashboardOverview
        {
            TodayStats = todayStats,
            ContentStats = contentStats,
            StorageStats = storageStats,
            ServiceStatuses = GetServiceStatuses(),
            Notifications = notifications,
            VisitorTrends = trends
        };
    }

    /// <summary>
    /// 获取访客统计
    /// </summary>
    private async Task<ResultVisitorStats> GetVisitorStatsAsync(DateTime date)
    {
        var result = await _db.QueryFirstOrDefaultAsync<dynamic>(
            DashboardSql.GetVisitorStatsToday,
            new { Date = date });

        return new ResultVisitorStats
        {
            PvCount = result?.pv_count ?? 0,
            UvCount = result?.uv_count ?? 0
        };
    }

    /// <summary>
    /// 获取内容统计
    /// </summary>
    private async Task<ResultContentStats> GetContentStatsAsync()
    {
        var articleStatsTask = _db.QueryFirstOrDefaultAsync<dynamic>(DashboardSql.GetArticleStats);
        var projectStatsTask = _db.QueryFirstOrDefaultAsync<dynamic>(DashboardSql.GetProjectStats);
        var courseStatsTask = _db.QueryFirstOrDefaultAsync<dynamic>(DashboardSql.GetCourseStats);

        await Task.WhenAll(articleStatsTask, projectStatsTask, courseStatsTask);

        var articleStats = await articleStatsTask;
        var projectStats = await projectStatsTask;
        var courseStats = await courseStatsTask;

        return new ResultContentStats
        {
            Articles = new ResultModuleStats
            {
                TotalCount = (int)(articleStats?.total_count ?? 0),
                PublishedCount = (int)(articleStats?.published_count ?? 0),
                DraftCount = (int)(articleStats?.draft_count ?? 0),
                TotalViews = articleStats?.total_views ?? 0
            },
            Projects = new ResultModuleStats
            {
                TotalCount = (int)(projectStats?.total_count ?? 0),
                PublishedCount = (int)(projectStats?.published_count ?? 0),
                DraftCount = (int)(projectStats?.draft_count ?? 0),
                TotalViews = projectStats?.total_views ?? 0
            },
            Courses = new ResultModuleStats
            {
                TotalCount = (int)(courseStats?.total_count ?? 0),
                PublishedCount = (int)(courseStats?.published_count ?? 0),
                DraftCount = (int)(courseStats?.draft_count ?? 0),
                TotalViews = courseStats?.total_plays ?? 0
            }
        };
    }

    /// <summary>
    /// 获取存储统计
    /// </summary>
    private async Task<ResultStorageStats> GetStorageStatsAsync()
    {
        var result = await _db.QueryFirstOrDefaultAsync<dynamic>(DashboardSql.GetFileStats);

        var totalSize = result?.total_size ?? 0L;
        var maxSize = 10L * 1024 * 1024 * 1024; // 默认 10GB 限制

        return new ResultStorageStats
        {
            TotalFiles = (int)(result?.total_count ?? 0),
            TotalSize = maxSize,
            UsedSize = totalSize,
            UsagePercent = maxSize > 0 ? Math.Round((double)totalSize / maxSize * 100, 2) : 0,
            FormattedTotalSize = FormatFileSize(maxSize),
            FormattedUsedSize = FormatFileSize(totalSize)
        };
    }

    /// <summary>
    /// 获取未读通知
    /// </summary>
    private async Task<IReadOnlyList<ResultTodoNotification>> GetUnreadNotificationsAsync(int limit)
    {
        var notifications = await _db.QueryAsync<TodoNotificationEntity>(
            DashboardSql.GetUnreadNotifications,
            new { Limit = limit });

        return notifications.Select(n => new ResultTodoNotification
        {
            Id = n.Id,
            Title = n.Title,
            Content = n.Content,
            Type = n.Type,
            Level = n.Level,
            SourceModule = n.SourceModule,
            SourceId = n.SourceId,
            ActionUrl = n.ActionUrl,
            IsRead = n.IsRead,
            CreatedAt = n.CreatedAt
        }).ToList();
    }

    /// <summary>
    /// 获取访问趋势
    /// </summary>
    private async Task<IReadOnlyList<ResultVisitorTrend>> GetVisitorTrendsAsync(DateTime startDate, DateTime endDate)
    {
        var trends = await _db.QueryAsync<dynamic>(
            DashboardSql.GetVisitorStatsTrend,
            new { StartDate = startDate, EndDate = endDate });

        return trends.Select(t => new ResultVisitorTrend
        {
            Date = t.stat_date,
            PvCount = t.pv_count ?? 0,
            UvCount = t.uv_count ?? 0
        }).ToList();
    }

    /// <summary>
    /// 获取服务状态（模拟）
    /// </summary>
    private static IReadOnlyList<ResultServiceStatus> GetServiceStatuses()
    {
        // MVP 版本返回静态状态，后续可对接实际健康检查
        return
        [
            new ResultServiceStatus
            {
                ServiceName = "用户服务",
                ServiceCode = "user-service",
                Status = ServiceHealthStatus.Healthy,
                LastCheckTime = DateTime.UtcNow
            },
            new ResultServiceStatus
            {
                ServiceName = "安全服务",
                ServiceCode = "security-service",
                Status = ServiceHealthStatus.Healthy,
                LastCheckTime = DateTime.UtcNow
            },
            new ResultServiceStatus
            {
                ServiceName = "附件服务",
                ServiceCode = "file-service",
                Status = ServiceHealthStatus.Healthy,
                LastCheckTime = DateTime.UtcNow
            },
            new ResultServiceStatus
            {
                ServiceName = "博客服务",
                ServiceCode = "blog-service",
                Status = ServiceHealthStatus.Healthy,
                LastCheckTime = DateTime.UtcNow
            },
            new ResultServiceStatus
            {
                ServiceName = "项目服务",
                ServiceCode = "project-service",
                Status = ServiceHealthStatus.Healthy,
                LastCheckTime = DateTime.UtcNow
            },
            new ResultServiceStatus
            {
                ServiceName = "教育服务",
                ServiceCode = "academy-service",
                Status = ServiceHealthStatus.Healthy,
                LastCheckTime = DateTime.UtcNow
            }
        ];
    }

    private static double CalculateGrowthRate(long current, long previous)
    {
        if (previous == 0) return current > 0 ? 100 : 0;
        return Math.Round((double)(current - previous) / previous * 100, 2);
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}
