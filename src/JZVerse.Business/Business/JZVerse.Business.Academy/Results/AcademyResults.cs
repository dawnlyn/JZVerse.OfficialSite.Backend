using JZVerse.Business.Academy.Database;

namespace JZVerse.Business.Academy.Results;

/// <summary>
/// 课程详情结果
/// </summary>
public sealed record ResultCourseDetail
{
    public Guid Id { get; init; }
    public string Title { get; init; } = "";
    public string? Summary { get; init; }
    public string? Description { get; init; }
    public string? CoverImage { get; init; }
    public Guid CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public CourseDifficulty Difficulty { get; init; }
    public int TotalDuration { get; init; }
    public int ChapterCount { get; init; }
    public int LessonCount { get; init; }
    public string? InstructorName { get; init; }
    public CourseStatus Status { get; init; }
    public int ViewCount { get; init; }
    public int PlayCount { get; init; }
    public bool IsRecommended { get; init; }
    public DateTime? PublishedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public IReadOnlyList<ResultChapter> Chapters { get; init; } = [];
    public IReadOnlyList<ResultMaterial> Materials { get; init; } = [];
}

/// <summary>
/// 课程列表项结果
/// </summary>
public sealed record ResultCourseItem
{
    public Guid Id { get; init; }
    public string Title { get; init; } = "";
    public string? Summary { get; init; }
    public string? CoverImage { get; init; }
    public Guid CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public CourseDifficulty Difficulty { get; init; }
    public int TotalDuration { get; init; }
    public int ChapterCount { get; init; }
    public int LessonCount { get; init; }
    public string? InstructorName { get; init; }
    public CourseStatus Status { get; init; }
    public int ViewCount { get; init; }
    public int PlayCount { get; init; }
    public bool IsRecommended { get; init; }
    public DateTime? PublishedAt { get; init; }
}

/// <summary>
/// 章节结果
/// </summary>
public sealed record ResultChapter
{
    public Guid Id { get; init; }
    public string Title { get; init; } = "";
    public int SortOrder { get; init; }
    public int LessonCount { get; init; }
    public int TotalDuration { get; init; }
    public IReadOnlyList<ResultLesson> Lessons { get; init; } = [];
}

/// <summary>
/// 课时结果
/// </summary>
public sealed record ResultLesson
{
    public Guid Id { get; init; }
    public string Title { get; init; } = "";
    public int Duration { get; init; }
    public string? VideoUrl { get; init; }
    public int SortOrder { get; init; }
    public bool IsFree { get; init; }
    public int PlayCount { get; init; }
}

/// <summary>
/// 资料结果
/// </summary>
public sealed record ResultMaterial
{
    public Guid Id { get; init; }
    public string Title { get; init; } = "";
    public Guid FileId { get; init; }
    public string? FileType { get; init; }
    public int DownloadCount { get; init; }
}

/// <summary>
/// 分类结果
/// </summary>
public sealed record ResultCourseCategory
{
    public Guid Id { get; init; }
    public Guid? ParentId { get; init; }
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public int SortOrder { get; init; }
    public int CourseCount { get; init; }
    public IReadOnlyList<ResultCourseCategory> Children { get; init; } = [];
}

/// <summary>
/// 在线教育首页数据结果
/// </summary>
public sealed record ResultAcademyHome
{
    public IReadOnlyList<ResultCourseItem> RecommendedCourses { get; init; } = [];
    public IReadOnlyList<ResultCourseItem> LatestCourses { get; init; } = [];
    public IReadOnlyList<ResultCourseItem> HotCourses { get; init; } = [];
    public IReadOnlyList<ResultCourseCategory> Categories { get; init; } = [];
    public int TotalCourseCount { get; init; }
    public long TotalPlayCount { get; init; }
}
