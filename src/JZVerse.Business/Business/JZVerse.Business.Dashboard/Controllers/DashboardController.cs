using JZVerse.Business.Dashboard.Results;
using JZVerse.Business.Dashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JZVerse.Business.Dashboard.Controllers;

/// <summary>
/// 仪表盘控制器 - 首页数据
/// </summary>
[ApiController]
[Route("api/v1/dashboard")]
[Authorize(Roles = "admin")]
public sealed class DashboardController : ControllerBase
{
    private readonly DashboardService _dashboardService;

    public DashboardController(DashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// 获取仪表盘概览数据
    /// </summary>
    [HttpGet("overview")]
    public async Task<ResultDashboardOverview> GetOverviewAsync()
    {
        return await _dashboardService.GetOverviewAsync();
    }
}
