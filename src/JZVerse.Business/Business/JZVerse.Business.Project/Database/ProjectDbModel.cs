namespace JZVerse.Business.Project.Database;

/// <summary>
/// 项目实体
/// </summary>
public sealed record ProjectEntity
{
    /// <summary>
    /// 项目唯一标识
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 项目名称
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// 项目简介
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// 项目详细描述
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 封面图片 URL
    /// </summary>
    public string? CoverImage { get; init; }

    /// <summary>
    /// 所属分类 ID
    /// </summary>
    public Guid CategoryId { get; init; }

    /// <summary>
    /// 技术栈（多个技术用逗号分隔）
    /// </summary>
    public string? TechStack { get; init; }

    /// <summary>
    /// 开发周期描述
    /// </summary>
    public string? DevelopmentCycle { get; init; }

    /// <summary>
    /// 源码地址
    /// </summary>
    public string? SourceUrl { get; init; }

    /// <summary>
    /// 演示地址
    /// </summary>
    public string? DemoUrl { get; init; }

    /// <summary>
    /// 项目状态
    /// </summary>
    public ProjectStatus Status { get; init; }

    /// <summary>
    /// 项目进度
    /// </summary>
    public ProjectProgress Progress { get; init; }

    /// <summary>
    /// 浏览次数
    /// </summary>
    public int ViewCount { get; init; }

    /// <summary>
    /// 是否推荐
    /// </summary>
    public bool IsRecommended { get; init; }

    /// <summary>
    /// 作者 ID
    /// </summary>
    public Guid AuthorId { get; init; }

    /// <summary>
    /// 作者名称（冗余字段，便于查询展示）
    /// </summary>
    public string? AuthorName { get; init; }

    /// <summary>
    /// 发布时间
    /// </summary>
    public DateTime? PublishedAt { get; init; }

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
/// 项目状态
/// </summary>
public enum ProjectStatus
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
    Offline = 2
}

/// <summary>
/// 项目进度
/// </summary>
public enum ProjectProgress
{
    /// <summary>
    /// 规划中
    /// </summary>
    Planning = 1,

    /// <summary>
    /// 开发中
    /// </summary>
    Developing = 2,

    /// <summary>
    /// 已完成
    /// </summary>
    Completed = 3,

    /// <summary>
    /// 维护中
    /// </summary>
    Maintaining = 4
}

/// <summary>
/// 项目分类实体
/// </summary>
public sealed record ProjectCategoryEntity
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
    /// 排序序号
    /// </summary>
    public int SortOrder { get; init; }

    /// <summary>
    /// 该分类下的项目数量（统计冗余字段）
    /// </summary>
    public int ProjectCount { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 软删除时间
    /// </summary>
    public DateTime? DeletedAt { get; init; }
}

/// <summary>
/// 项目标签实体
/// </summary>
public sealed record ProjectTagEntity
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
    /// 使用该标签的项目数量（统计冗余字段）
    /// </summary>
    public int ProjectCount { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }
}
