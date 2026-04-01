using JZVerse.Business.Abstractions.Models;
using JZVerse.Business.Academy.Arguments;
using JZVerse.Business.Academy.Database;
using JZVerse.Business.Academy.Results;
using JZVerse.MicroHuaxia.DataAccess.Abstractions;

namespace JZVerse.Business.Academy.Services;

/// <summary>
/// 课程服务
/// </summary>
public sealed class CourseService
{
    private readonly IDbExecutor _db;
    private readonly ISnowflakeIdGenerator _idGenerator;

    public CourseService(IDbExecutor db, ISnowflakeIdGenerator idGenerator)
    {
        _db = db;
        _idGenerator = idGenerator;
    }

    /// <summary>
    /// 保存课程
    /// </summary>
    public async Task<Guid> SaveAsync(ArgSaveCourse arg, Guid instructorId, string? instructorName)
    {
        var isNew = !arg.Id.HasValue || arg.Id.Value == Guid.Empty;
        var courseId = isNew ? _idGenerator.GenerateGuid() : arg.Id!.Value;

        if (isNew)
        {
            await _db.ExecuteAsync(AcademySql.CreateCourse, new CourseEntity
            {
                Id = courseId,
                Title = arg.Title,
                Summary = arg.Summary,
                Description = arg.Description,
                CoverImage = arg.CoverImage,
                CategoryId = arg.CategoryId,
                Difficulty = (CourseDifficulty)arg.Difficulty,
                TotalDuration = 0,
                ChapterCount = 0,
                LessonCount = 0,
                InstructorId = instructorId,
                InstructorName = instructorName,
                Status = CourseStatus.Draft,
                ViewCount = 0,
                PlayCount = 0,
                IsRecommended = arg.IsRecommended,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            await _db.ExecuteAsync(AcademySql.UpdateCourse, new
            {
                Id = courseId,
                arg.Title,
                arg.Summary,
                arg.Description,
                arg.CoverImage,
                arg.CategoryId,
                Difficulty = arg.Difficulty,
                arg.IsRecommended,
                UpdatedAt = DateTime.UtcNow
            });
        }

        return courseId;
    }

    /// <summary>
    /// 获取课程详情
    /// </summary>
    public async Task<ResultCourseDetail?> GetByIdAsync(Guid id, bool incrementView = false)
    {
        var course = await _db.QueryFirstOrDefaultAsync<CourseEntity>(AcademySql.GetCourseById, new { Id = id });
        if (course is null) return null;

        if (incrementView)
        {
            await _db.ExecuteAsync(AcademySql.IncrementViewCount, new { Id = id });
        }

        var categories = await _db.QueryAsync<CourseCategoryEntity>(AcademySql.GetAllCategories);
        var category = categories.FirstOrDefault(c => c.Id == course.CategoryId);

        var chapters = await _db.QueryAsync<ChapterEntity>(AcademySql.GetChaptersByCourse, new { CourseId = id });
        var lessons = await _db.QueryAsync<LessonEntity>(AcademySql.GetLessonsByCourse, new { CourseId = id });
        var materials = await _db.QueryAsync<CourseMaterialEntity>(AcademySql.GetMaterialsByCourse, new { CourseId = id });

        return new ResultCourseDetail
        {
            Id = course.Id,
            Title = course.Title,
            Summary = course.Summary,
            Description = course.Description,
            CoverImage = course.CoverImage,
            CategoryId = course.CategoryId,
            CategoryName = category?.Name,
            Difficulty = course.Difficulty,
            TotalDuration = course.TotalDuration,
            ChapterCount = course.ChapterCount,
            LessonCount = course.LessonCount,
            InstructorName = course.InstructorName,
            Status = course.Status,
            ViewCount = course.ViewCount + (incrementView ? 1 : 0),
            PlayCount = course.PlayCount,
            IsRecommended = course.IsRecommended,
            PublishedAt = course.PublishedAt,
            CreatedAt = course.CreatedAt,
            Chapters = chapters.Select(ch => new ResultChapter
            {
                Id = ch.Id,
                Title = ch.Title,
                SortOrder = ch.SortOrder,
                LessonCount = ch.LessonCount,
                TotalDuration = ch.TotalDuration,
                Lessons = lessons.Where(l => l.ChapterId == ch.Id)
                    .OrderBy(l => l.SortOrder)
                    .Select(l => new ResultLesson
                    {
                        Id = l.Id,
                        Title = l.Title,
                        Duration = l.Duration,
                        VideoUrl = l.VideoUrl,
                        SortOrder = l.SortOrder,
                        IsFree = l.IsFree,
                        PlayCount = l.PlayCount
                    }).ToList()
            }).ToList(),
            Materials = materials.Select(m => new ResultMaterial
            {
                Id = m.Id,
                Title = m.Title,
                FileId = m.FileId,
                FileType = m.FileType,
                DownloadCount = m.DownloadCount
            }).ToList()
        };
    }

    /// <summary>
    /// 获取课程列表（后台）
    /// </summary>
    public async Task<PagedResult<ResultCourseItem>> GetListAdminAsync(ArgQueryCoursesAdmin arg)
    {
        var offset = (arg.PageIndex - 1) * arg.PageSize;

        var courses = await _db.QueryAsync<CourseEntity>(AcademySql.GetCourseListAdmin, new
        {
            arg.Status,
            arg.CategoryId,
            arg.Difficulty,
            arg.Keyword,
            Limit = arg.PageSize,
            Offset = offset
        });

        var total = await _db.ExecuteScalarAsync<int>(AcademySql.GetCourseCount, new
        {
            arg.Status,
            arg.CategoryId,
            arg.Difficulty,
            arg.Keyword
        });

        var categories = await _db.QueryAsync<CourseCategoryEntity>(AcademySql.GetAllCategories);

        return new PagedResult<ResultCourseItem>
        {
            Items = courses.Select(c => MapToCourseItem(c, categories)).ToList(),
            TotalCount = total,
            PageIndex = arg.PageIndex,
            PageSize = arg.PageSize
        };
    }

    /// <summary>
    /// 获取课程列表（前端）
    /// </summary>
    public async Task<PagedResult<ResultCourseItem>> GetListPublicAsync(ArgQueryCoursesPublic arg)
    {
        var offset = (arg.PageIndex - 1) * arg.PageSize;

        var courses = await _db.QueryAsync<CourseEntity>(AcademySql.GetCourseListPublic, new
        {
            arg.CategoryId,
            arg.Difficulty,
            arg.Keyword,
            Limit = arg.PageSize,
            Offset = offset
        });

        var total = await _db.ExecuteScalarAsync<int>(AcademySql.GetCourseCount, new
        {
            Status = 1,
            arg.CategoryId,
            arg.Difficulty,
            arg.Keyword
        });

        var categories = await _db.QueryAsync<CourseCategoryEntity>(AcademySql.GetAllCategories);

        return new PagedResult<ResultCourseItem>
        {
            Items = courses.Select(c => MapToCourseItem(c, categories)).ToList(),
            TotalCount = total,
            PageIndex = arg.PageIndex,
            PageSize = arg.PageSize
        };
    }

    /// <summary>
    /// 发布课程
    /// </summary>
    public async Task<bool> PublishAsync(Guid id)
    {
        var affected = await _db.ExecuteAsync(AcademySql.PublishCourse, new
        {
            Id = id,
            PublishedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        return affected > 0;
    }

    /// <summary>
    /// 下架课程
    /// </summary>
    public async Task<bool> OfflineAsync(Guid id)
    {
        var affected = await _db.ExecuteAsync(AcademySql.OfflineCourse, new
        {
            Id = id,
            UpdatedAt = DateTime.UtcNow
        });
        return affected > 0;
    }

    /// <summary>
    /// 删除课程
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id)
    {
        var affected = await _db.ExecuteAsync(AcademySql.SoftDeleteCourse, new
        {
            Id = id,
            DeletedAt = DateTime.UtcNow
        });
        return affected > 0;
    }

    /// <summary>
    /// 获取首页数据
    /// </summary>
    public async Task<ResultAcademyHome> GetHomeDataAsync()
    {
        var recommended = await _db.QueryAsync<CourseEntity>(AcademySql.GetRecommendedCourses, new { Limit = 6 });
        var latest = await _db.QueryAsync<CourseEntity>(AcademySql.GetCourseListPublic, new
        {
            CategoryId = (long?)null,
            Difficulty = (int?)null,
            Keyword = (string?)null,
            Limit = 8,
            Offset = 0
        });
        var hot = await _db.QueryAsync<CourseEntity>(AcademySql.GetHotCourses, new { Limit = 6 });
        var categories = await _db.QueryAsync<CourseCategoryEntity>(AcademySql.GetAllCategories);

        var totalCount = await _db.ExecuteScalarAsync<int>(AcademySql.GetCourseCount, new
        {
            Status = 1,
            CategoryId = (long?)null,
            Difficulty = (int?)null,
            Keyword = (string?)null
        });

        return new ResultAcademyHome
        {
            RecommendedCourses = recommended.Select(c => MapToCourseItem(c, categories)).ToList(),
            LatestCourses = latest.Select(c => MapToCourseItem(c, categories)).ToList(),
            HotCourses = hot.Select(c => MapToCourseItem(c, categories)).ToList(),
            Categories = BuildCategoryTree(categories),
            TotalCourseCount = totalCount,
            TotalPlayCount = latest.Sum(c => c.PlayCount)
        };
    }

    /// <summary>
    /// 播放课时
    /// </summary>
    public async Task<ResultLesson?> PlayLessonAsync(Guid lessonId)
    {
        var lesson = await _db.QueryFirstOrDefaultAsync<LessonEntity>(AcademySql.GetLessonById, new { Id = lessonId });
        if (lesson is null) return null;

        await _db.ExecuteAsync(AcademySql.IncrementLessonPlayCount, new { Id = lessonId });

        return new ResultLesson
        {
            Id = lesson.Id,
            Title = lesson.Title,
            Duration = lesson.Duration,
            VideoUrl = lesson.VideoUrl,
            SortOrder = lesson.SortOrder,
            IsFree = lesson.IsFree,
            PlayCount = lesson.PlayCount + 1
        };
    }

    private static ResultCourseItem MapToCourseItem(CourseEntity course, IEnumerable<CourseCategoryEntity> categories)
    {
        var category = categories.FirstOrDefault(c => c.Id == course.CategoryId);
        return new ResultCourseItem
        {
            Id = course.Id,
            Title = course.Title,
            Summary = course.Summary,
            CoverImage = course.CoverImage,
            CategoryId = course.CategoryId,
            CategoryName = category?.Name,
            Difficulty = course.Difficulty,
            TotalDuration = course.TotalDuration,
            ChapterCount = course.ChapterCount,
            LessonCount = course.LessonCount,
            InstructorName = course.InstructorName,
            Status = course.Status,
            ViewCount = course.ViewCount,
            PlayCount = course.PlayCount,
            IsRecommended = course.IsRecommended,
            PublishedAt = course.PublishedAt
        };
    }

    private static IReadOnlyList<ResultCourseCategory> BuildCategoryTree(IEnumerable<CourseCategoryEntity> categories)
    {
        var lookup = categories.ToLookup(c => c.ParentId);

        IReadOnlyList<ResultCourseCategory> BuildChildren(Guid? parentId)
        {
            return lookup[parentId].Select(c => new ResultCourseCategory
            {
                Id = c.Id,
                ParentId = c.ParentId,
                Name = c.Name,
                Description = c.Description,
                Icon = c.Icon,
                SortOrder = c.SortOrder,
                CourseCount = c.CourseCount,
                Children = BuildChildren(c.Id)
            }).ToList();
        }

        return BuildChildren(null);
    }
}
