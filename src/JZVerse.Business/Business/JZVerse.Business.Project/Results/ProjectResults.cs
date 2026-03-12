using JZVerse.Business.Project.Database;

namespace JZVerse.Business.Project.Results;

/// <summary>
/// 项目详情结果
/// </summary>
public sealed record ResultProjectDetail
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string? Summary { get; init; }
    public string? Description { get; init; }
    public string? CoverImage { get; init; }
    public Guid CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public string? TechStack { get; init; }
    public string? DevelopmentCycle { get; init; }
    public string? SourceUrl { get; init; }
    public string? DemoUrl { get; init; }
    public ProjectStatus Status { get; init; }
    public ProjectProgress Progress { get; init; }
    public int ViewCount { get; init; }
    public bool IsRecommended { get; init; }
    public string? AuthorName { get; init; }
    public DateTime? PublishedAt { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// 项目列表项结果
/// </summary>
public sealed record ResultProjectItem
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string? Summary { get; init; }
    public string? CoverImage { get; init; }
    public Guid CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public string? TechStack { get; init; }
    public ProjectStatus Status { get; init; }
    public ProjectProgress Progress { get; init; }
    public int ViewCount { get; init; }
    public bool IsRecommended { get; init; }
    public DateTime? PublishedAt { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// 项目分类结果
/// </summary>
public sealed record ResultProjectCategory
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public int SortOrder { get; init; }
    public int ProjectCount { get; init; }
}

/// <summary>
/// 项目首页数据结果
/// </summary>
public sealed record ResultProjectHome
{
    public IReadOnlyList<ResultProjectItem> RecommendedProjects { get; init; } = [];
    public IReadOnlyList<ResultProjectItem> LatestProjects { get; init; } = [];
    public IReadOnlyList<ResultProjectCategory> Categories { get; init; } = [];
}
