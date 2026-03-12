using JZVerse.Business.Abstractions.Authentication;
using JZVerse.Business.Abstractions.Models;
using JZVerse.Business.Academy.Arguments;
using JZVerse.Business.Academy.Results;
using JZVerse.Business.Academy.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JZVerse.Business.Academy.Controllers;

/// <summary>
/// 课程控制器
/// </summary>
[ApiController]
[Route("api/v1/academy")]
public sealed class CourseController : ControllerBase
{
    private readonly CourseService _courseService;
    private readonly IUserContext _userContext;

    public CourseController(CourseService courseService, IUserContext userContext)
    {
        _courseService = courseService;
        _userContext = userContext;
    }

    /// <summary>
    /// 获取首页数据
    /// </summary>
    [HttpGet("home")]
    public async Task<ResultAcademyHome> GetHomeAsync()
    {
        return await _courseService.GetHomeDataAsync();
    }

    /// <summary>
    /// 获取课程列表（前端）
    /// </summary>
    [HttpGet("courses")]
    public async Task<PagedResult<ResultCourseItem>> GetListAsync([FromQuery] ArgQueryCoursesPublic arg)
    {
        return await _courseService.GetListPublicAsync(arg);
    }

    /// <summary>
    /// 获取课程详情（前端）
    /// </summary>
    [HttpGet("courses/{id}")]
    public async Task<ResultCourseDetail?> GetByIdAsync(Guid id)
    {
        return await _courseService.GetByIdAsync(id, incrementView: true);
    }

    /// <summary>
    /// 播放课时
    /// </summary>
    [HttpGet("lessons/{lessonId}/play")]
    public async Task<ResultLesson?> PlayLessonAsync(Guid lessonId)
    {
        return await _courseService.PlayLessonAsync(lessonId);
    }

    /// <summary>
    /// 获取课程列表（后台）
    /// </summary>
    [HttpGet("admin/courses")]
    [Authorize]
    public async Task<PagedResult<ResultCourseItem>> GetListAdminAsync([FromQuery] ArgQueryCoursesAdmin arg)
    {
        return await _courseService.GetListAdminAsync(arg);
    }

    /// <summary>
    /// 获取课程详情（后台）
    /// </summary>
    [HttpGet("admin/courses/{id}")]
    [Authorize]
    public async Task<ResultCourseDetail?> GetByIdAdminAsync(Guid id)
    {
        return await _courseService.GetByIdAsync(id, incrementView: false);
    }

    /// <summary>
    /// 保存课程
    /// </summary>
    [HttpPost("admin/courses")]
    [Authorize]
    public async Task<Guid> SaveAsync([FromBody] ArgSaveCourse arg)
    {
        return await _courseService.SaveAsync(arg, _userContext.UserId, _userContext.Username);
    }

    /// <summary>
    /// 发布课程
    /// </summary>
    [HttpPut("admin/courses/{id}/publish")]
    [Authorize]
    public async Task<bool> PublishAsync(Guid id)
    {
        return await _courseService.PublishAsync(id);
    }

    /// <summary>
    /// 下架课程
    /// </summary>
    [HttpPut("admin/courses/{id}/offline")]
    [Authorize]
    public async Task<bool> OfflineAsync(Guid id)
    {
        return await _courseService.OfflineAsync(id);
    }

    /// <summary>
    /// 删除课程
    /// </summary>
    [HttpDelete("admin/courses/{id}")]
    [Authorize]
    public async Task<bool> DeleteAsync(Guid id)
    {
        return await _courseService.DeleteAsync(id);
    }
}
