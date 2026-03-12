using JZVerse.Business.Blog.Database;

namespace JZVerse.Business.Blog.Results;

/// <summary>
/// 文章详情结果
/// </summary>
public sealed record ResultArticleDetail
{
    public Guid Id { get; init; }
    public string Title { get; init; } = "";
    public string? Summary { get; init; }
    public string Content { get; init; } = "";
    public string? CoverImage { get; init; }
    public Guid CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public string? AuthorName { get; init; }
    public ArticleStatus Status { get; init; }
    public int ViewCount { get; init; }
    public int LikeCount { get; init; }
    public int CommentCount { get; init; }
    public bool IsRecommended { get; init; }
    public bool IsTop { get; init; }
    public DateTime? PublishedAt { get; init; }
    public DateTime? ScheduledAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public IReadOnlyList<ResultTag> Tags { get; init; } = [];
}

/// <summary>
/// 文章列表项结果
/// </summary>
public sealed record ResultArticleItem
{
    public Guid Id { get; init; }
    public string Title { get; init; } = "";
    public string? Summary { get; init; }
    public string? CoverImage { get; init; }
    public Guid CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public string? AuthorName { get; init; }
    public ArticleStatus Status { get; init; }
    public int ViewCount { get; init; }
    public int LikeCount { get; init; }
    public int CommentCount { get; init; }
    public bool IsRecommended { get; init; }
    public bool IsTop { get; init; }
    public DateTime? PublishedAt { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// 分类结果
/// </summary>
public sealed record ResultCategory
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public int SortOrder { get; init; }
    public int ArticleCount { get; init; }
}

/// <summary>
/// 标签结果
/// </summary>
public sealed record ResultTag
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string? Color { get; init; }
    public int ArticleCount { get; init; }
}

/// <summary>
/// 博客首页数据结果
/// </summary>
public sealed record ResultBlogHome
{
    public IReadOnlyList<ResultArticleItem> RecommendedArticles { get; init; } = [];
    public IReadOnlyList<ResultArticleItem> LatestArticles { get; init; } = [];
    public IReadOnlyList<ResultArticleItem> HotArticles { get; init; } = [];
    public IReadOnlyList<ResultCategory> Categories { get; init; } = [];
    public IReadOnlyList<ResultTag> Tags { get; init; } = [];
}
