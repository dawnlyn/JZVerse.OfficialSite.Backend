namespace JZVerse.Business.Blog.Database;

/// <summary>
/// 文章实体
/// </summary>
public sealed record ArticleEntity
{
    /// <summary>
    /// 文章唯一标识
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 文章标题
    /// </summary>
    public string Title { get; init; } = "";

    /// <summary>
    /// 文章摘要
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// 文章正文内容
    /// </summary>
    public string Content { get; init; } = "";

    /// <summary>
    /// 封面图片 URL
    /// </summary>
    public string? CoverImage { get; init; }

    /// <summary>
    /// 所属分类 ID
    /// </summary>
    public Guid CategoryId { get; init; }

    /// <summary>
    /// 作者 ID
    /// </summary>
    public Guid AuthorId { get; init; }

    /// <summary>
    /// 作者名称（冗余字段，便于查询展示）
    /// </summary>
    public string? AuthorName { get; init; }

    /// <summary>
    /// 文章状态
    /// </summary>
    public ArticleStatus Status { get; init; }

    /// <summary>
    /// 浏览次数
    /// </summary>
    public int ViewCount { get; init; }

    /// <summary>
    /// 点赞次数
    /// </summary>
    public int LikeCount { get; init; }

    /// <summary>
    /// 评论次数
    /// </summary>
    public int CommentCount { get; init; }

    /// <summary>
    /// 是否推荐
    /// </summary>
    public bool IsRecommended { get; init; }

    /// <summary>
    /// 是否置顶
    /// </summary>
    public bool IsTop { get; init; }

    /// <summary>
    /// 发布时间
    /// </summary>
    public DateTime? PublishedAt { get; init; }

    /// <summary>
    /// 定时发布时间
    /// </summary>
    public DateTime? ScheduledAt { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime? UpdatedAt { get; init; }

    /// <summary>
    /// 软删除时间
    /// </summary>
    public DateTime? DeletedAt { get; init; }
}

/// <summary>
/// 文章状态
/// </summary>
public enum ArticleStatus
{
    /// <summary>
    /// 草稿
    /// </summary>
    Draft = 0,

    /// <summary>
    /// 已发布
    /// </summary>
    Published = 1,

    /// <summary>
    /// 已下架
    /// </summary>
    Offline = 2,

    /// <summary>
    /// 定时发布
    /// </summary>
    Scheduled = 3,
}

/// <summary>
/// 文章分类实体
/// </summary>
public sealed record CategoryEntity
{
    /// <summary>
    /// 分类唯一标识
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 分类名称
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// 分类描述
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 分类图标
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>
    /// 排序序号
    /// </summary>
    public int SortOrder { get; init; }

    /// <summary>
    /// 该分类下的文章数量（统计冗余字段）
    /// </summary>
    public int ArticleCount { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime? UpdatedAt { get; init; }

    /// <summary>
    /// 软删除时间
    /// </summary>
    public DateTime? DeletedAt { get; init; }
}

/// <summary>
/// 文章标签实体
/// </summary>
public sealed record TagEntity
{
    /// <summary>
    /// 标签唯一标识
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 标签名称
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// 标签颜色（十六进制颜色值）
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// 使用该标签的文章数量（统计冗余字段）
    /// </summary>
    public int ArticleCount { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// 文章标签关联实体
/// </summary>
public sealed record ArticleTagEntity
{
    /// <summary>
    /// 文章 ID
    /// </summary>
    public Guid ArticleId { get; init; }

    /// <summary>
    /// 标签 ID
    /// </summary>
    public Guid TagId { get; init; }

    /// <summary>
    /// 关联创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }
}
