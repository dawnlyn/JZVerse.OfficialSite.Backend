using JZVerse.Business.Abstractions.Authentication;
using JZVerse.Business.Abstractions.Models;
using JZVerse.Business.Blog.Arguments;
using JZVerse.Business.Blog.Results;
using JZVerse.Business.Blog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JZVerse.Business.Blog.Controllers;

/// <summary>
/// 文章控制器
/// </summary>
[ApiController]
[Route("api/v1/blog")]
public sealed class ArticleController : ControllerBase
{
    private readonly ArticleService _articleService;
    private readonly IUserContext _userContext;

    public ArticleController(ArticleService articleService, IUserContext userContext)
    {
        _articleService = articleService;
        _userContext = userContext;
    }

    /// <summary>
    /// 获取博客首页数据
    /// </summary>
    [HttpGet("home")]
    public async Task<ResultBlogHome> GetHomeAsync()
    {
        return await _articleService.GetHomeDataAsync();
    }

    /// <summary>
    /// 获取文章列表（前端）
    /// </summary>
    [HttpGet("articles")]
    public async Task<PagedResult<ResultArticleItem>> GetListAsync([FromQuery] ArgQueryArticlesPublic arg)
    {
        return await _articleService.GetListPublicAsync(arg);
    }

    /// <summary>
    /// 获取文章详情（前端，增加浏览量）
    /// </summary>
    [HttpGet("articles/{id}")]
    public async Task<ResultArticleDetail?> GetByIdAsync(Guid id)
    {
        return await _articleService.GetByIdAsync(id, incrementView: true);
    }

    /// <summary>
    /// 获取文章列表（后台管理）
    /// </summary>
    [HttpGet("admin/articles")]
    [Authorize]
    public async Task<PagedResult<ResultArticleItem>> GetListAdminAsync([FromQuery] ArgQueryArticlesAdmin arg)
    {
        return await _articleService.GetListAdminAsync(arg);
    }

    /// <summary>
    /// 获取文章详情（后台管理，不增加浏览量）
    /// </summary>
    [HttpGet("admin/articles/{id}")]
    [Authorize]
    public async Task<ResultArticleDetail?> GetByIdAdminAsync(Guid id)
    {
        return await _articleService.GetByIdAsync(id, incrementView: false);
    }

    /// <summary>
    /// 保存文章
    /// </summary>
    [HttpPost("admin/articles")]
    [Authorize]
    public async Task<Guid> SaveAsync([FromBody] ArgSaveArticle arg)
    {
        return await _articleService.SaveAsync(arg, _userContext.UserId, _userContext.Username);
    }

    /// <summary>
    /// 发布文章
    /// </summary>
    [HttpPut("admin/articles/{id}/publish")]
    [Authorize]
    public async Task<bool> PublishAsync(Guid id)
    {
        return await _articleService.PublishAsync(id);
    }

    /// <summary>
    /// 下架文章
    /// </summary>
    [HttpPut("admin/articles/{id}/offline")]
    [Authorize]
    public async Task<bool> OfflineAsync(Guid id)
    {
        return await _articleService.OfflineAsync(id);
    }

    /// <summary>
    /// 删除文章
    /// </summary>
    [HttpDelete("admin/articles/{id}")]
    [Authorize]
    public async Task<bool> DeleteAsync(Guid id)
    {
        return await _articleService.DeleteAsync(id);
    }
}
