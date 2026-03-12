using JZVerse.Business.Abstractions.Models;
using JZVerse.Business.Dashboard.Arguments;
using JZVerse.Business.Dashboard.Database;
using JZVerse.Business.Dashboard.Results;
using JZVerse.DataAccess.Abstractions;

namespace JZVerse.Business.Dashboard.Services;

/// <summary>
/// 待办通知服务
/// </summary>
public sealed class NotificationService
{
    private readonly IDbExecutor _db;
    private readonly ISnowflakeIdGenerator _idGenerator;

    public NotificationService(IDbExecutor db, ISnowflakeIdGenerator idGenerator)
    {
        _db = db;
        _idGenerator = idGenerator;
    }

    /// <summary>
    /// 创建通知
    /// </summary>
    public async Task<Guid> CreateAsync(
        string title,
        string? content,
        TodoNotificationType type,
        TodoNotificationLevel level,
        string? sourceModule = null,
        string? sourceId = null,
        string? actionUrl = null)
    {
        var notification = new TodoNotificationEntity
        {
            Id = _idGenerator.GenerateGuid(),
            Title = title,
            Content = content,
            Type = type,
            Level = level,
            SourceModule = sourceModule,
            SourceId = sourceId,
            ActionUrl = actionUrl,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        await _db.ExecuteAsync(DashboardSql.CreateNotification, notification);
        return notification.Id;
    }

    /// <summary>
    /// 获取通知详情
    /// </summary>
    public async Task<ResultTodoNotification?> GetByIdAsync(Guid id)
    {
        var notification = await _db.QueryFirstOrDefaultAsync<TodoNotificationEntity>(
            DashboardSql.GetNotificationById,
            new { Id = id });

        return notification is null ? null : MapToResult(notification);
    }

    /// <summary>
    /// 获取未读通知列表
    /// </summary>
    public async Task<IReadOnlyList<ResultTodoNotification>> GetUnreadAsync(int limit = 20)
    {
        var notifications = await _db.QueryAsync<TodoNotificationEntity>(
            DashboardSql.GetUnreadNotifications,
            new { Limit = limit });

        return notifications.Select(MapToResult).ToList();
    }

    /// <summary>
    /// 查询通知列表
    /// </summary>
    public async Task<PagedResult<ResultTodoNotification>> GetListAsync(ArgQueryNotifications arg)
    {
        var offset = (arg.PageIndex - 1) * arg.PageSize;

        var notifications = await _db.QueryAsync<TodoNotificationEntity>(DashboardSql.GetNotifications, new
        {
            arg.Type,
            arg.Level,
            arg.IsRead,
            Limit = arg.PageSize,
            Offset = offset
        });

        var total = await _db.ExecuteScalarAsync<int>(DashboardSql.GetNotificationCount, new
        {
            arg.Type,
            arg.Level,
            arg.IsRead
        });

        return new PagedResult<ResultTodoNotification>
        {
            Items = notifications.Select(MapToResult).ToList(),
            TotalCount = total,
            PageIndex = arg.PageIndex,
            PageSize = arg.PageSize
        };
    }

    /// <summary>
    /// 获取未读统计
    /// </summary>
    public async Task<ResultUnreadStats> GetUnreadStatsAsync()
    {
        var total = await _db.ExecuteScalarAsync<int>(DashboardSql.GetUnreadCount);

        var unreadNotifications = await _db.QueryAsync<TodoNotificationEntity>(
            DashboardSql.GetUnreadNotifications,
            new { Limit = 1000 });

        var grouped = unreadNotifications.GroupBy(n => n.Type).ToDictionary(g => g.Key, g => g.Count());

        return new ResultUnreadStats
        {
            TotalUnread = total,
            SecurityAlerts = grouped.GetValueOrDefault(TodoNotificationType.SecurityAlert, 0),
            StorageAlerts = grouped.GetValueOrDefault(TodoNotificationType.StorageAlert, 0),
            LoginAlerts = grouped.GetValueOrDefault(TodoNotificationType.LoginAlert, 0),
            OtherAlerts = grouped.GetValueOrDefault(TodoNotificationType.ConfigChange, 0) +
                         grouped.GetValueOrDefault(TodoNotificationType.ContentReview, 0)
        };
    }

    /// <summary>
    /// 标记为已读
    /// </summary>
    public async Task<bool> MarkAsReadAsync(Guid id, Guid readBy)
    {
        var affected = await _db.ExecuteAsync(DashboardSql.MarkNotificationAsRead, new
        {
            Id = id,
            ReadBy = readBy,
            ReadAt = DateTime.UtcNow
        });

        return affected > 0;
    }

    /// <summary>
    /// 标记所有为已读
    /// </summary>
    public async Task<int> MarkAllAsReadAsync(Guid readBy)
    {
        return await _db.ExecuteAsync(DashboardSql.MarkAllNotificationsAsRead, new
        {
            ReadBy = readBy,
            ReadAt = DateTime.UtcNow
        });
    }

    private static ResultTodoNotification MapToResult(TodoNotificationEntity entity) => new()
    {
        Id = entity.Id,
        Title = entity.Title,
        Content = entity.Content,
        Type = entity.Type,
        Level = entity.Level,
        SourceModule = entity.SourceModule,
        SourceId = entity.SourceId,
        ActionUrl = entity.ActionUrl,
        IsRead = entity.IsRead,
        CreatedAt = entity.CreatedAt
    };
}
