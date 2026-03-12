using JZVerse.Business.Abstractions.Authentication;
using JZVerse.Business.Abstractions.Models;
using JZVerse.Business.Project.Arguments;
using JZVerse.Business.Project.Results;
using JZVerse.Business.Project.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JZVerse.Business.Project.Controllers;

/// <summary>
/// 项目控制器
/// </summary>
[ApiController]
[Route("api/v1/projects")]
public sealed class ProjectController : ControllerBase
{
    private readonly ProjectService _projectService;
    private readonly IUserContext _userContext;

    public ProjectController(ProjectService projectService, IUserContext userContext)
    {
        _projectService = projectService;
        _userContext = userContext;
    }

    /// <summary>
    /// 获取首页数据
    /// </summary>
    [HttpGet("home")]
    public async Task<ResultProjectHome> GetHomeAsync()
    {
        return await _projectService.GetHomeDataAsync();
    }

    /// <summary>
    /// 获取项目列表（前端）
    /// </summary>
    [HttpGet]
    public async Task<PagedResult<ResultProjectItem>> GetListAsync([FromQuery] ArgQueryProjectsPublic arg)
    {
        return await _projectService.GetListPublicAsync(arg);
    }

    /// <summary>
    /// 获取项目详情（前端）
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ResultProjectDetail?> GetByIdAsync(Guid id)
    {
        return await _projectService.GetByIdAsync(id, incrementView: true);
    }

    /// <summary>
    /// 获取项目列表（后台）
    /// </summary>
    [HttpGet("admin")]
    [Authorize]
    public async Task<PagedResult<ResultProjectItem>> GetListAdminAsync([FromQuery] ArgQueryProjectsAdmin arg)
    {
        return await _projectService.GetListAdminAsync(arg);
    }

    /// <summary>
    /// 获取项目详情（后台）
    /// </summary>
    [HttpGet("admin/{id}")]
    [Authorize]
    public async Task<ResultProjectDetail?> GetByIdAdminAsync(Guid id)
    {
        return await _projectService.GetByIdAsync(id, incrementView: false);
    }

    /// <summary>
    /// 保存项目
    /// </summary>
    [HttpPost("admin")]
    [Authorize]
    public async Task<Guid> SaveAsync([FromBody] ArgSaveProject arg)
    {
        return await _projectService.SaveAsync(arg, _userContext.UserId, _userContext.Username);
    }

    /// <summary>
    /// 发布项目
    /// </summary>
    [HttpPut("admin/{id}/publish")]
    [Authorize]
    public async Task<bool> PublishAsync(Guid id)
    {
        return await _projectService.PublishAsync(id);
    }

    /// <summary>
    /// 下架项目
    /// </summary>
    [HttpPut("admin/{id}/offline")]
    [Authorize]
    public async Task<bool> OfflineAsync(Guid id)
    {
        return await _projectService.OfflineAsync(id);
    }

    /// <summary>
    /// 删除项目
    /// </summary>
    [HttpDelete("admin/{id}")]
    [Authorize]
    public async Task<bool> DeleteAsync(Guid id)
    {
        return await _projectService.DeleteAsync(id);
    }
}
