namespace JZVerse.Business.Project.Database;

/// <summary>
/// 项目公示 SQL 语句
/// </summary>
public static class ProjectSql
{
    /// <summary>
    /// 创建项目
    /// </summary>
    public const string CreateProject = """
        INSERT INTO projects (id, name, summary, description, cover_image, category_id, tech_stack, development_cycle,
                             source_url, demo_url, status, progress, view_count, is_recommended, author_id, author_name, published_at, created_at)
        VALUES (@Id, @Name, @Summary, @Description, @CoverImage, @CategoryId, @TechStack, @DevelopmentCycle,
                @SourceUrl, @DemoUrl, @Status, @Progress, @ViewCount, @IsRecommended, @AuthorId, @AuthorName, @PublishedAt, @CreatedAt)
        """;

    /// <summary>
    /// 根据 ID 获取项目
    /// </summary>
    public const string GetProjectById = """
        SELECT id, name, summary, description, cover_image, category_id, tech_stack, development_cycle,
               source_url, demo_url, status, progress, view_count, is_recommended, author_id, author_name, published_at, created_at, updated_at
        FROM projects
        WHERE id = @Id AND deleted_at IS NULL
        """;

    /// <summary>
    /// 获取项目列表（后台）
    /// </summary>
    public const string GetProjectListAdmin = """
        SELECT id, name, summary, cover_image, category_id, tech_stack, status, progress, view_count, is_recommended, published_at, created_at
        FROM projects
        WHERE deleted_at IS NULL
          AND (@Status IS NULL OR status = @Status)
          AND (@CategoryId IS NULL OR category_id = @CategoryId)
          AND (@Progress IS NULL OR progress = @Progress)
          AND (@Keyword IS NULL OR name ILIKE '%' || @Keyword || '%')
        ORDER BY created_at DESC
        LIMIT @Limit OFFSET @Offset
        """;

    /// <summary>
    /// 获取项目列表（前端）
    /// </summary>
    public const string GetProjectListPublic = """
        SELECT id, name, summary, cover_image, category_id, tech_stack, progress, view_count, is_recommended, published_at
        FROM projects
        WHERE status = 1 AND deleted_at IS NULL
          AND (@CategoryId IS NULL OR category_id = @CategoryId)
          AND (@Progress IS NULL OR progress = @Progress)
          AND (@Keyword IS NULL OR name ILIKE '%' || @Keyword || '%' OR tech_stack ILIKE '%' || @Keyword || '%')
        ORDER BY is_recommended DESC, published_at DESC
        LIMIT @Limit OFFSET @Offset
        """;

    /// <summary>
    /// 获取项目总数
    /// </summary>
    public const string GetProjectCount = """
        SELECT COUNT(*) FROM projects
        WHERE deleted_at IS NULL
          AND (@Status IS NULL OR status = @Status)
          AND (@CategoryId IS NULL OR category_id = @CategoryId)
          AND (@Progress IS NULL OR progress = @Progress)
          AND (@Keyword IS NULL OR name ILIKE '%' || @Keyword || '%')
        """;

    /// <summary>
    /// 更新项目
    /// </summary>
    public const string UpdateProject = """
        UPDATE projects
        SET name = @Name, summary = @Summary, description = @Description, cover_image = @CoverImage,
            category_id = @CategoryId, tech_stack = @TechStack, development_cycle = @DevelopmentCycle,
            source_url = @SourceUrl, demo_url = @DemoUrl, progress = @Progress, is_recommended = @IsRecommended, updated_at = @UpdatedAt
        WHERE id = @Id AND deleted_at IS NULL
        """;

    /// <summary>
    /// 发布项目
    /// </summary>
    public const string PublishProject = """
        UPDATE projects
        SET status = 1, published_at = @PublishedAt, updated_at = @UpdatedAt
        WHERE id = @Id AND deleted_at IS NULL
        """;

    /// <summary>
    /// 下架项目
    /// </summary>
    public const string OfflineProject = """
        UPDATE projects
        SET status = 2, updated_at = @UpdatedAt
        WHERE id = @Id AND deleted_at IS NULL
        """;

    /// <summary>
    /// 软删除项目
    /// </summary>
    public const string SoftDeleteProject = """
        UPDATE projects
        SET deleted_at = @DeletedAt
        WHERE id = @Id
        """;

    /// <summary>
    /// 增加浏览量
    /// </summary>
    public const string IncrementViewCount = """
        UPDATE projects SET view_count = view_count + 1 WHERE id = @Id
        """;

    /// <summary>
    /// 获取推荐项目
    /// </summary>
    public const string GetRecommendedProjects = """
        SELECT id, name, summary, cover_image, category_id, tech_stack, progress, view_count, published_at
        FROM projects
        WHERE status = 1 AND is_recommended = TRUE AND deleted_at IS NULL
        ORDER BY published_at DESC
        LIMIT @Limit
        """;

    /// <summary>
    /// 获取所有分类
    /// </summary>
    public const string GetAllCategories = """
        SELECT id, name, description, sort_order, project_count, created_at
        FROM project_categories
        WHERE deleted_at IS NULL
        ORDER BY sort_order ASC
        """;

    /// <summary>
    /// 创建分类
    /// </summary>
    public const string CreateCategory = """
        INSERT INTO project_categories (id, name, description, sort_order, project_count, created_at)
        VALUES (@Id, @Name, @Description, @SortOrder, @ProjectCount, @CreatedAt)
        """;
}
