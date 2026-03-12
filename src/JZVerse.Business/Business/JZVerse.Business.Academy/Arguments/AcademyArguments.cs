using JZVerse.Business.Abstractions.Validation;

namespace JZVerse.Business.Academy.Arguments;

/// <summary>
/// 保存课程入参
/// </summary>
public sealed record ArgSaveCourse
{
    public Guid? Id { get; init; }

    [Required(ErrorMessage = "课程标题不能为空")]
    [StringLength(MinLength = 1, MaxLength = 100, ErrorMessage = "课程标题长度必须在1-100之间")]
    public string Title { get; init; } = "";

    [StringLength(MaxLength = 300, ErrorMessage = "简介最大300字")]
    public string? Summary { get; init; }

    public string? Description { get; init; }
    public string? CoverImage { get; init; }

    public Guid CategoryId { get; init; }

    [Range(1, 3, ErrorMessage = "请选择难度")]
    public int Difficulty { get; init; } = 1;

    public bool IsRecommended { get; init; }
}

/// <summary>
/// 保存章节入参
/// </summary>
public sealed record ArgSaveChapter
{
    public Guid? Id { get; init; }

    [Required(ErrorMessage = "课程ID不能为空")]
    public Guid CourseId { get; init; }

    [Required(ErrorMessage = "章节标题不能为空")]
    [StringLength(MinLength = 1, MaxLength = 100, ErrorMessage = "章节标题长度必须在1-100之间")]
    public string Title { get; init; } = "";

    public int SortOrder { get; init; }
}

/// <summary>
/// 保存课时入参
/// </summary>
public sealed record ArgSaveLesson
{
    public Guid? Id { get; init; }

    [Required(ErrorMessage = "课程ID不能为空")]
    public Guid CourseId { get; init; }

    [Required(ErrorMessage = "章节ID不能为空")]
    public Guid ChapterId { get; init; }

    [Required(ErrorMessage = "课时标题不能为空")]
    [StringLength(MinLength = 1, MaxLength = 100, ErrorMessage = "课时标题长度必须在1-100之间")]
    public string Title { get; init; } = "";

    [Range(0, int.MaxValue, ErrorMessage = "时长不能为负数")]
    public int Duration { get; init; }

    public Guid? VideoFileId { get; init; }
    public string? VideoUrl { get; init; }
    public int SortOrder { get; init; }
    public bool IsFree { get; init; }
}

/// <summary>
/// 查询课程列表入参（后台）
/// </summary>
public sealed record ArgQueryCoursesAdmin
{
    public int? Status { get; init; }
    public Guid? CategoryId { get; init; }
    public int? Difficulty { get; init; }
    public string? Keyword { get; init; }

    [Range(1, 100, ErrorMessage = "每页数量必须在1-100之间")]
    public int PageSize { get; init; } = 20;

    [Range(1, int.MaxValue, ErrorMessage = "页码必须大于0")]
    public int PageIndex { get; init; } = 1;
}

/// <summary>
/// 查询课程列表入参（前端）
/// </summary>
public sealed record ArgQueryCoursesPublic
{
    public Guid? CategoryId { get; init; }
    public int? Difficulty { get; init; }
    public string? Keyword { get; init; }
    public string? SortBy { get; init; } = "latest";

    [Range(1, 50, ErrorMessage = "每页数量必须在1-50之间")]
    public int PageSize { get; init; } = 12;

    [Range(1, int.MaxValue, ErrorMessage = "页码必须大于0")]
    public int PageIndex { get; init; } = 1;
}
