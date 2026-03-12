using JZVerse.Business.Abstractions.Models;
using JZVerse.Business.Central.User.Arguments;
using JZVerse.Business.Central.User.Results;
using JZVerse.Business.Central.User.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JZVerse.Business.Central.User.Controllers;

/// <summary>
/// 访客统计控制器
/// </summary>
[ApiController]
[Route("api/v1/visitor")]
public sealed class VisitorController : ControllerBase
{
    private readonly VisitorStatsService _visitorStatsService;

    public VisitorController(VisitorStatsService visitorStatsService)
    {
        _visitorStatsService = visitorStatsService;
    }

    /// <summary>
    /// 记录访客行为日志（前端调用）
    /// </summary>
    [HttpPost("log")]
    public async Task<Guid> RecordLogAsync([FromBody] ArgRecordVisitorLog arg)
    {
        return await _visitorStatsService.RecordVisitorLogAsync(arg);
    }

    /// <summary>
    /// 更新访客退出信息（前端调用）
    /// </summary>
    [HttpPut("log/exit")]
    public async Task UpdateExitAsync([FromBody] ArgUpdateVisitorExit arg)
    {
        await _visitorStatsService.UpdateVisitorExitAsync(arg);
    }

    /// <summary>
    /// 记录用户行为（前端调用）
    /// </summary>
    [HttpPost("behavior")]
    public async Task RecordBehaviorAsync([FromBody] ArgRecordUserBehavior arg)
    {
        await _visitorStatsService.RecordUserBehaviorAsync(arg);
    }

    /// <summary>
    /// 获取访客统计（后台管理）
    /// </summary>
    [HttpGet("stats")]
    [Authorize]
    public async Task<ResultVisitorStatsSummary> GetStatsAsync([FromQuery] ArgQueryVisitorStats arg)
    {
        return await _visitorStatsService.GetStatsAsync(arg);
    }

    /// <summary>
    /// 获取访客行为日志列表（后台管理）
    /// </summary>
    [HttpGet("logs")]
    [Authorize]
    public async Task<PagedResult<ResultVisitorLog>> GetLogsAsync([FromQuery] ArgQueryVisitorLogs arg)
    {
        return await _visitorStatsService.GetVisitorLogsAsync(arg);
    }

    /// <summary>
    /// 获取热门内容（后台管理）
    /// </summary>
    [HttpGet("hot/{module}")]
    [Authorize]
    public async Task<IReadOnlyList<ResultHotContent>> GetHotContentAsync(string module, [FromQuery] int limit = 10)
    {
        return await _visitorStatsService.GetHotContentAsync(module, limit);
    }

    /// <summary>
    /// 获取数据看板（后台管理首页）
    /// </summary>
    [HttpGet("dashboard")]
    [Authorize]
    public async Task<ResultDashboard> GetDashboardAsync()
    {
        return await _visitorStatsService.GetDashboardAsync();
    }
}
