using JZVerse.Business.Abstractions.Authentication;
using JZVerse.Business.Central.User.Arguments;
using JZVerse.Business.Central.User.Database;
using JZVerse.Business.Central.User.Results;
using JZVerse.Business.Central.User.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JZVerse.Business.Central.User.Controllers;

/// <summary>
/// 管理员控制器
/// </summary>
[ApiController]
[Route("api/v1/admin")]
public sealed class AdminController : ControllerBase
{
    private readonly AdminService _adminService;
    private readonly IUserContext _userContext;

    public AdminController(AdminService adminService, IUserContext userContext)
    {
        _adminService = adminService;
        _userContext = userContext;
    }

    /// <summary>
    /// 管理员登录
    /// </summary>
    [HttpPost("login")]
    public async Task<ResultAdminLogin> LoginAsync([FromBody] ArgAdminLogin arg)
    {
        var loginIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var loginDevice = HttpContext.Request.Headers.UserAgent.ToString();
        return await _adminService.LoginAsync(arg, loginIp, loginDevice);
    }

    /// <summary>
    /// 获取当前管理员信息
    /// </summary>
    [HttpGet("profile")]
    [Authorize]
    public async Task<ResultAdminInfo?> GetProfileAsync()
    {
        return await _adminService.GetByIdAsync(_userContext.UserId);
    }

    /// <summary>
    /// 更新管理员密码
    /// </summary>
    [HttpPut("password")]
    [Authorize]
    public async Task<bool> UpdatePasswordAsync([FromBody] ArgUpdateAdminPassword arg)
    {
        return await _adminService.UpdatePasswordAsync(_userContext.UserId, arg);
    }

    /// <summary>
    /// 更新管理员资料
    /// </summary>
    [HttpPut("profile")]
    [Authorize]
    public async Task<bool> UpdateProfileAsync([FromBody] ArgUpdateAdminProfile arg)
    {
        return await _adminService.UpdateProfileAsync(_userContext.UserId, arg);
    }

    /// <summary>
    /// 创建管理员（仅超级管理员）
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "superadmin")]
    public async Task<ResultAdminInfo> CreateAsync([FromBody] ArgCreateAdmin arg)
    {
        return await _adminService.CreateAsync(arg);
    }

    /// <summary>
    /// 获取管理员列表（仅超级管理员）
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "superadmin")]
    public async Task<Abstractions.Models.PagedResult<ResultAdminInfo>> GetListAsync(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        return await _adminService.GetListAsync(pageIndex, pageSize);
    }

    /// <summary>
    /// 冻结管理员（仅超级管理员）
    /// </summary>
    [HttpPut("{id}/freeze")]
    [Authorize(Roles = "superadmin")]
    public async Task<bool> FreezeAsync(Guid id)
    {
        return await _adminService.UpdateStatusAsync(id, AdminStatus.Frozen);
    }

    /// <summary>
    /// 解冻管理员（仅超级管理员）
    /// </summary>
    [HttpPut("{id}/unfreeze")]
    [Authorize(Roles = "superadmin")]
    public async Task<bool> UnfreezeAsync(Guid id)
    {
        return await _adminService.UpdateStatusAsync(id, AdminStatus.Active);
    }
}
