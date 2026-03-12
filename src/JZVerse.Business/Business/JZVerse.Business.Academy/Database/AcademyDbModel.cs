namespace JZVerse.Business.Academy.Database;

/// <summary>
/// 课程实体
/// </summary>
public sealed record CourseEntity
{
    /// <summary>
    /// 课程唯一标识
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 课程标题
    /// </summary>
    public string Title { get; init; } = "";

    /// <summary>
    /// 课程简介
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// 课程详细描述
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
    /// 课程难度
    /// </summary>
    public CourseDifficulty Difficulty { get; init; }

    /// <summary>
    /// 总时长（秒）
    /// </summary>
    public int TotalDuration { get; init; }

    /// <summary>
    /// 章节数量
    /// </summary>
    public int ChapterCount { get; init; }

    /// <summary>
    /// 课时数量
    /// </summary>
    public int LessonCount { get; init; }

    /// <summary>
    /// 讲师 ID
    /// </summary>
    public Guid InstructorId { get; init; }

    /// <summary>
    /// 讲师名称（冗余字段，便于查询展示）
    /// </summary>
    public string? InstructorName { get; init; }

    /// <summary>
    /// 课程状态
    /// </summary>
    public CourseStatus Status { get; init; }

    /// <summary>
    /// 浏览次数
    /// </summary>
    public int ViewCount { get; init; }

    /// <summary>
    /// 播放次数
    /// </summary>
    public int PlayCount { get; init; }

    /// <summary>
    /// 是否推荐
    /// </summary>
    public bool IsRecommended { get; init; }

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
/// 课程状态
/// </summary>
public enum CourseStatus
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
/// 课程难度
/// </summary>
public enum CourseDifficulty
{
    /// <summary>
    /// 入门级
    /// </summary>
    Beginner = 1,

    /// <summary>
    /// 进阶级
    /// </summary>
    Intermediate = 2,

    /// <summary>
    /// 高级
    /// </summary>
    Advanced = 3
}

/// <summary>
/// 课程分类实体
/// </summary>
public sealed record CourseCategoryEntity
{
    /// <summary>
    /// 分类唯一标识
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 父级分类 ID（顶级分类为 null）
    /// </summary>
    public Guid? ParentId { get; init; }

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
    /// 该分类下的课程数量（统计冗余字段）
    /// </summary>
    public int CourseCount { get; init; }

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
/// 课程章节实体
/// </summary>
public sealed record ChapterEntity
{
    /// <summary>
    /// 章节唯一标识
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 所属课程 ID
    /// </summary>
    public Guid CourseId { get; init; }

    /// <summary>
    /// 章节标题
    /// </summary>
    public string Title { get; init; } = "";

    /// <summary>
    /// 排序序号
    /// </summary>
    public int SortOrder { get; init; }

    /// <summary>
    /// 课时数量
    /// </summary>
    public int LessonCount { get; init; }

    /// <summary>
    /// 章节总时长（秒）
    /// </summary>
    public int TotalDuration { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// 课时实体
/// </summary>
public sealed record LessonEntity
{
    /// <summary>
    /// 课时唯一标识
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 所属课程 ID
    /// </summary>
    public Guid CourseId { get; init; }

    /// <summary>
    /// 所属章节 ID
    /// </summary>
    public Guid ChapterId { get; init; }

    /// <summary>
    /// 课时标题
    /// </summary>
    public string Title { get; init; } = "";

    /// <summary>
    /// 课时时长（秒）
    /// </summary>
    public int Duration { get; init; }

    /// <summary>
    /// 视频文件 ID
    /// </summary>
    public Guid? VideoFileId { get; init; }

    /// <summary>
    /// 视频 URL（外链视频时使用）
    /// </summary>
    public string? VideoUrl { get; init; }

    /// <summary>
    /// 排序序号
    /// </summary>
    public int SortOrder { get; init; }

    /// <summary>
    /// 是否免费试看
    /// </summary>
    public bool IsFree { get; init; }

    /// <summary>
    /// 播放次数
    /// </summary>
    public int PlayCount { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// 课程资料实体
/// </summary>
public sealed record CourseMaterialEntity
{
    /// <summary>
    /// 资料唯一标识
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 所属课程 ID
    /// </summary>
    public Guid CourseId { get; init; }

    /// <summary>
    /// 所属课时 ID（课时级资料时使用）
    /// </summary>
    public Guid? LessonId { get; init; }

    /// <summary>
    /// 资料标题
    /// </summary>
    public string Title { get; init; } = "";

    /// <summary>
    /// 文件 ID
    /// </summary>
    public Guid FileId { get; init; }

    /// <summary>
    /// 文件类型
    /// </summary>
    public string? FileType { get; init; }

    /// <summary>
    /// 下载次数
    /// </summary>
    public int DownloadCount { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }
}
