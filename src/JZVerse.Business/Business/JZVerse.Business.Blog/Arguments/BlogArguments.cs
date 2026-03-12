using JZVerse.Business.Abstractions.Validation;

namespace JZVerse.Business.Blog.Arguments;

/// <summary>
/// 创建/更新文章入参
/// </summary>
public sealed record ArgSaveArticle
{
    public Guid? Id { get; init; }

    [Required(ErrorMessage = "标题不能为空")]
    [StringLength(MinLength = 1, MaxLength = 200, ErrorMessage = "标题长度必须在1-200之间")]
    public string Title { get; init; } = "";

    [StringLength(MaxLength = 500, ErrorMessage = "摘要最大500字")]
    public string? Summary { get; init; }

    [Required(ErrorMessage = "内容不能为空")]
    public string Content { get; init; } = "";

    public string? CoverImage { get; init; }

    public Guid CategoryId { get; init; }

    public string[]? Tags { get; init; }

    public bool IsRecommended { get; init; }

    public bool IsTop { get; init; }

    /// <summary>
    /// 定时发布时间
    /// </summary>
    public DateTime? ScheduledAt { get; init; }
}

/// <summary>
/// 查询文章列表入参（后台）
/// </summary>
public sealed record ArgQueryArticlesAdmin
{
    public int? Status { get; init; }
    public Guid? CategoryId { get; init; }
    public string? Keyword { get; init; }

    [Range(1, 100, ErrorMessage = "每页数量必须在1-100之间")]
    public int PageSize { get; init; } = 20;

    [Range(1, int.MaxValue, ErrorMessage = "页码必须大于0")]
    public int PageIndex { get; init; } = 1;
}

/// <summary>
/// 查询文章列表入参（前端）
/// </summary>
public sealed record ArgQueryArticlesPublic
{
    public Guid? CategoryId { get; init; }
    public Guid? TagId { get; init; }
    public string? Keyword { get; init; }
    public string? SortBy { get; init; } = "latest";

    [Range(1, 50, ErrorMessage = "每页数量必须在1-50之间")]
    public int PageSize { get; init; } = 10;

    [Range(1, int.MaxValue, ErrorMessage = "页码必须大于0")]
    public int PageIndex { get; init; } = 1;
}

/// <summary>
/// 创建/更新分类入参
/// </summary>
public sealed record ArgSaveCategory
{
    public Guid? Id { get; init; }

    [Required(ErrorMessage = "分类名称不能为空")]
    [StringLength(MinLength = 1, MaxLength = 50, ErrorMessage = "分类名称长度必须在1-50之间")]
    public string Name { get; init; } = "";

    [StringLength(MaxLength = 200, ErrorMessage = "描述最大200字")]
    public string? Description { get; init; }

    public string? Icon { get; init; }

    public int SortOrder { get; init; }
}

/// <summary>
/// 创建标签入参
/// </summary>
public sealed record ArgCreateTag
{
    [Required(ErrorMessage = "标签名称不能为空")]
    [StringLength(MinLength = 1, MaxLength = 30, ErrorMessage = "标签名称长度必须在1-30之间")]
    public string Name { get; init; } = "";

    public string? Color { get; init; }
}
