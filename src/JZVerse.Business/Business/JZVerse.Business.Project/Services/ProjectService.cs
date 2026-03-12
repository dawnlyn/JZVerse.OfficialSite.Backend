using JZVerse.Business.Abstractions.Models;
using JZVerse.Business.Project.Arguments;
using JZVerse.Business.Project.Database;
using JZVerse.Business.Project.Results;
using JZVerse.DataAccess.Abstractions;

namespace JZVerse.Business.Project.Services;

/// <summary>
/// 项目服务
/// </summary>
public sealed class ProjectService
{
    private readonly IDbExecutor _db;
    private readonly ISnowflakeIdGenerator _idGenerator;

    public ProjectService(IDbExecutor db, ISnowflakeIdGenerator idGenerator)
    {
        _db = db;
        _idGenerator = idGenerator;
    }

    /// <summary>
    /// 保存项目
    /// </summary>
    public async Task<Guid> SaveAsync(ArgSaveProject arg, Guid authorId, string? authorName)
    {
        var isNew = !arg.Id.HasValue || arg.Id.Value == Guid.Empty;
        var projectId = isNew ? _idGenerator.GenerateGuid() : arg.Id!.Value;

        if (isNew)
        {
            await _db.ExecuteAsync(ProjectSql.CreateProject, new ProjectEntity
            {
                Id = projectId,
                Name = arg.Name,
                Summary = arg.Summary,
                Description = arg.Description,
                CoverImage = arg.CoverImage,
                CategoryId = arg.CategoryId,
                TechStack = arg.TechStack,
                DevelopmentCycle = arg.DevelopmentCycle,
                SourceUrl = arg.SourceUrl,
                DemoUrl = arg.DemoUrl,
                Status = ProjectStatus.Draft,
                Progress = (ProjectProgress)arg.Progress,
                ViewCount = 0,
                IsRecommended = arg.IsRecommended,
                AuthorId = authorId,
                AuthorName = authorName,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            await _db.ExecuteAsync(ProjectSql.UpdateProject, new
            {
                Id = projectId,
                arg.Name,
                arg.Summary,
                arg.Description,
                arg.CoverImage,
                arg.CategoryId,
                arg.TechStack,
                arg.DevelopmentCycle,
                arg.SourceUrl,
                arg.DemoUrl,
                Progress = arg.Progress,
                arg.IsRecommended,
                UpdatedAt = DateTime.UtcNow
            });
        }

        return projectId;
    }

    /// <summary>
    /// 获取项目详情
    /// </summary>
    public async Task<ResultProjectDetail?> GetByIdAsync(Guid id, bool incrementView = false)
    {
        var project = await _db.QueryFirstOrDefaultAsync<ProjectEntity>(ProjectSql.GetProjectById, new { Id = id });
        if (project is null) return null;

        if (incrementView)
        {
            await _db.ExecuteAsync(ProjectSql.IncrementViewCount, new { Id = id });
        }

        var categories = await _db.QueryAsync<ProjectCategoryEntity>(ProjectSql.GetAllCategories);
        var category = categories.FirstOrDefault(c => c.Id == project.CategoryId);

        return new ResultProjectDetail
        {
            Id = project.Id,
            Name = project.Name,
            Summary = project.Summary,
            Description = project.Description,
            CoverImage = project.CoverImage,
            CategoryId = project.CategoryId,
            CategoryName = category?.Name,
            TechStack = project.TechStack,
            DevelopmentCycle = project.DevelopmentCycle,
            SourceUrl = project.SourceUrl,
            DemoUrl = project.DemoUrl,
            Status = project.Status,
            Progress = project.Progress,
            ViewCount = project.ViewCount + (incrementView ? 1 : 0),
            IsRecommended = project.IsRecommended,
            AuthorName = project.AuthorName,
            PublishedAt = project.PublishedAt,
            CreatedAt = project.CreatedAt
        };
    }

    /// <summary>
    /// 获取项目列表（后台）
    /// </summary>
    public async Task<PagedResult<ResultProjectItem>> GetListAdminAsync(ArgQueryProjectsAdmin arg)
    {
        var offset = (arg.PageIndex - 1) * arg.PageSize;

        var projects = await _db.QueryAsync<ProjectEntity>(ProjectSql.GetProjectListAdmin, new
        {
            arg.Status,
            arg.CategoryId,
            arg.Progress,
            arg.Keyword,
            Limit = arg.PageSize,
            Offset = offset
        });

        var total = await _db.ExecuteScalarAsync<int>(ProjectSql.GetProjectCount, new
        {
            arg.Status,
            arg.CategoryId,
            arg.Progress,
            arg.Keyword
        });

        var categories = await _db.QueryAsync<ProjectCategoryEntity>(ProjectSql.GetAllCategories);

        return new PagedResult<ResultProjectItem>
        {
            Items = projects.Select(p => MapToProjectItem(p, categories)).ToList(),
            TotalCount = total,
            PageIndex = arg.PageIndex,
            PageSize = arg.PageSize
        };
    }

    /// <summary>
    /// 获取项目列表（前端）
    /// </summary>
    public async Task<PagedResult<ResultProjectItem>> GetListPublicAsync(ArgQueryProjectsPublic arg)
    {
        var offset = (arg.PageIndex - 1) * arg.PageSize;

        var projects = await _db.QueryAsync<ProjectEntity>(ProjectSql.GetProjectListPublic, new
        {
            arg.CategoryId,
            arg.Progress,
            arg.Keyword,
            Limit = arg.PageSize,
            Offset = offset
        });

        var total = await _db.ExecuteScalarAsync<int>(ProjectSql.GetProjectCount, new
        {
            Status = 1,
            arg.CategoryId,
            arg.Progress,
            arg.Keyword
        });

        var categories = await _db.QueryAsync<ProjectCategoryEntity>(ProjectSql.GetAllCategories);

        return new PagedResult<ResultProjectItem>
        {
            Items = projects.Select(p => MapToProjectItem(p, categories)).ToList(),
            TotalCount = total,
            PageIndex = arg.PageIndex,
            PageSize = arg.PageSize
        };
    }

    /// <summary>
    /// 发布项目
    /// </summary>
    public async Task<bool> PublishAsync(Guid id)
    {
        var affected = await _db.ExecuteAsync(ProjectSql.PublishProject, new
        {
            Id = id,
            PublishedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        return affected > 0;
    }

    /// <summary>
    /// 下架项目
    /// </summary>
    public async Task<bool> OfflineAsync(Guid id)
    {
        var affected = await _db.ExecuteAsync(ProjectSql.OfflineProject, new
        {
            Id = id,
            UpdatedAt = DateTime.UtcNow
        });
        return affected > 0;
    }

    /// <summary>
    /// 删除项目
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id)
    {
        var affected = await _db.ExecuteAsync(ProjectSql.SoftDeleteProject, new
        {
            Id = id,
            DeletedAt = DateTime.UtcNow
        });
        return affected > 0;
    }

    /// <summary>
    /// 获取首页数据
    /// </summary>
    public async Task<ResultProjectHome> GetHomeDataAsync()
    {
        var recommended = await _db.QueryAsync<ProjectEntity>(ProjectSql.GetRecommendedProjects, new { Limit = 6 });
        var latest = await _db.QueryAsync<ProjectEntity>(ProjectSql.GetProjectListPublic, new
        {
            CategoryId = (long?)null,
            Progress = (int?)null,
            Keyword = (string?)null,
            Limit = 12,
            Offset = 0
        });
        var categories = await _db.QueryAsync<ProjectCategoryEntity>(ProjectSql.GetAllCategories);

        return new ResultProjectHome
        {
            RecommendedProjects = recommended.Select(p => MapToProjectItem(p, categories)).ToList(),
            LatestProjects = latest.Select(p => MapToProjectItem(p, categories)).ToList(),
            Categories = categories.Select(c => new ResultProjectCategory
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                SortOrder = c.SortOrder,
                ProjectCount = c.ProjectCount
            }).ToList()
        };
    }

    private static ResultProjectItem MapToProjectItem(ProjectEntity project, IEnumerable<ProjectCategoryEntity> categories)
    {
        var category = categories.FirstOrDefault(c => c.Id == project.CategoryId);
        return new ResultProjectItem
        {
            Id = project.Id,
            Name = project.Name,
            Summary = project.Summary,
            CoverImage = project.CoverImage,
            CategoryId = project.CategoryId,
            CategoryName = category?.Name,
            TechStack = project.TechStack,
            Status = project.Status,
            Progress = project.Progress,
            ViewCount = project.ViewCount,
            IsRecommended = project.IsRecommended,
            PublishedAt = project.PublishedAt,
            CreatedAt = project.CreatedAt
        };
    }
}
