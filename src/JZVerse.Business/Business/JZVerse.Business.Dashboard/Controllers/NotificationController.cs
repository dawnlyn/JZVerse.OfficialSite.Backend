using JZVerse.Business.Abstractions.Authentication;
using JZVerse.Business.Abstractions.Models;
using JZVerse.Business.Dashboard.Arguments;
using JZVerse.Business.Dashboard.Results;
using JZVerse.Business.Dashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JZVerse.Business.Dashboard.Controllers;

/// <summary>
/// 待办通知控制器
/// </summary>
[ApiController]
[Route("api/v1/dashboard/notification")]
[Authorize(Roles = "admin")]
public sealed class NotificationController : ControllerBase
{
    private readonly NotificationService _notificationService;
    private readonly IUserContext _userContext;

    public NotificationController(NotificationService notificationService, IUserContext userContext)
    {
        _notificationService = notificationService;
        _userContext = userContext;
    }

    /// <summary>
    /// 获取通知详情
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ResultTodoNotification?> GetByIdAsync(Guid id)
    {
        return await _notificationService.GetByIdAsync(id);
    }

    /// <summary>
    /// 获取未读通知列表
    /// </summary>
    [HttpGet("unread")]
    public async Task<IReadOnlyList<ResultTodoNotification>> GetUnreadAsync([FromQuery] int limit = 20)
    {
        return await _notificationService.GetUnreadAsync(limit);
    }

    /// <summary>
    /// 查询通知列表
    /// </summary>
    [HttpGet]
    public async Task<PagedResult<ResultTodoNotification>> GetListAsync([FromQuery] ArgQueryNotifications arg)
    {
        return await _notificationService.GetListAsync(arg);
    }

    /// <summary>
    /// 获取未读统计
    /// </summary>
    [HttpGet("unread/stats")]
    public async Task<ResultUnreadStats> GetUnreadStatsAsync()
    {
        return await _notificationService.GetUnreadStatsAsync();
    }

    /// <summary>
    /// 标记为已读
    /// </summary>
    [HttpPut("{id:guid}/read")]
    public async Task<bool> MarkAsReadAsync(Guid id)
    {
        return await _notificationService.MarkAsReadAsync(id, _userContext.UserId);
    }

    /// <summary>
    /// 标记所有为已读
    /// </summary>
    [HttpPut("read-all")]
    public async Task<int> MarkAllAsReadAsync()
    {
        return await _notificationService.MarkAllAsReadAsync(_userContext.UserId);
    }
}
