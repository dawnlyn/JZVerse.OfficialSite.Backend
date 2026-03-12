namespace JZVerse.Business.Blog.Database;

/// <summary>
/// 博客系统 SQL 语句
/// </summary>
public static class BlogSql
{
    #region 文章相关

    /// <summary>
    /// 创建文章
    /// </summary>
    public const string CreateArticle = """
        INSERT INTO articles (id, title, summary, content, cover_image, category_id, author_id, author_name,
                             status, view_count, like_count, comment_count, is_recommended, is_top, published_at, scheduled_at, created_at)
        VALUES (@Id, @Title, @Summary, @Content, @CoverImage, @CategoryId, @AuthorId, @AuthorName,
                @Status, @ViewCount, @LikeCount, @CommentCount, @IsRecommended, @IsTop, @PublishedAt, @ScheduledAt, @CreatedAt)
        """;

    /// <summary>
    /// 根据 ID 获取文章
    /// </summary>
    public const string GetArticleById = """
        SELECT id, title, summary, content, cover_image, category_id, author_id, author_name,
               status, view_count, like_count, comment_count, is_recommended, is_top, published_at, scheduled_at, created_at, updated_at
        FROM articles
        WHERE id = @Id AND deleted_at IS NULL
        """;

    /// <summary>
    /// 获取文章列表（后台管理）
    /// </summary>
    public const string GetArticleListAdmin = """
        SELECT id, title, summary, cover_image, category_id, author_id, author_name,
               status, view_count, like_count, comment_count, is_recommended, is_top, published_at, created_at, updated_at
        FROM articles
        WHERE deleted_at IS NULL
          AND (@Status IS NULL OR status = @Status)
          AND (@CategoryId IS NULL OR category_id = @CategoryId)
          AND (@Keyword IS NULL OR title ILIKE '%' || @Keyword || '%')
        ORDER BY is_top DESC, created_at DESC
        LIMIT @Limit OFFSET @Offset
        """;

    /// <summary>
    /// 获取文章列表（前端展示）
    /// </summary>
    public const string GetArticleListPublic = """
        SELECT id, title, summary, cover_image, category_id, author_name,
               view_count, like_count, comment_count, is_recommended, is_top, published_at
        FROM articles
        WHERE status = 1 AND deleted_at IS NULL
          AND (@CategoryId IS NULL OR category_id = @CategoryId)
          AND (@TagId IS NULL OR id IN (SELECT article_id FROM article_tags WHERE tag_id = @TagId))
          AND (@Keyword IS NULL OR title ILIKE '%' || @Keyword || '%' OR content ILIKE '%' || @Keyword || '%')
        ORDER BY is_top DESC, published_at DESC
        LIMIT @Limit OFFSET @Offset
        """;

    /// <summary>
    /// 获取文章总数
    /// </summary>
    public const string GetArticleCount = """
        SELECT COUNT(*) FROM articles
        WHERE deleted_at IS NULL
          AND (@Status IS NULL OR status = @Status)
          AND (@CategoryId IS NULL OR category_id = @CategoryId)
          AND (@Keyword IS NULL OR title ILIKE '%' || @Keyword || '%')
        """;

    /// <summary>
    /// 获取公开文章总数
    /// </summary>
    public const string GetArticleCountPublic = """
        SELECT COUNT(*) FROM articles
        WHERE status = 1 AND deleted_at IS NULL
          AND (@CategoryId IS NULL OR category_id = @CategoryId)
          AND (@TagId IS NULL OR id IN (SELECT article_id FROM article_tags WHERE tag_id = @TagId))
          AND (@Keyword IS NULL OR title ILIKE '%' || @Keyword || '%' OR content ILIKE '%' || @Keyword || '%')
        """;

    /// <summary>
    /// 更新文章
    /// </summary>
    public const string UpdateArticle = """
        UPDATE articles
        SET title = @Title, summary = @Summary, content = @Content, cover_image = @CoverImage,
            category_id = @CategoryId, is_recommended = @IsRecommended, is_top = @IsTop,
            scheduled_at = @ScheduledAt, updated_at = @UpdatedAt
        WHERE id = @Id AND deleted_at IS NULL
        """;

    /// <summary>
    /// 发布文章
    /// </summary>
    public const string PublishArticle = """
        UPDATE articles
        SET status = 1, published_at = @PublishedAt, updated_at = @UpdatedAt
        WHERE id = @Id AND deleted_at IS NULL
        """;

    /// <summary>
    /// 下架文章
    /// </summary>
    public const string OfflineArticle = """
        UPDATE articles
        SET status = 2, updated_at = @UpdatedAt
        WHERE id = @Id AND deleted_at IS NULL
        """;

    /// <summary>
    /// 软删除文章
    /// </summary>
    public const string SoftDeleteArticle = """
        UPDATE articles
        SET deleted_at = @DeletedAt
        WHERE id = @Id
        """;

    /// <summary>
    /// 增加浏览量
    /// </summary>
    public const string IncrementViewCount = """
        UPDATE articles
        SET view_count = view_count + 1
        WHERE id = @Id
        """;

    /// <summary>
    /// 获取推荐文章
    /// </summary>
    public const string GetRecommendedArticles = """
        SELECT id, title, summary, cover_image, category_id, author_name, view_count, published_at
        FROM articles
        WHERE status = 1 AND is_recommended = TRUE AND deleted_at IS NULL
        ORDER BY published_at DESC
        LIMIT @Limit
        """;

    /// <summary>
    /// 获取热门文章
    /// </summary>
    public const string GetHotArticles = """
        SELECT id, title, summary, cover_image, category_id, author_name, view_count, published_at
        FROM articles
        WHERE status = 1 AND deleted_at IS NULL AND published_at >= @StartTime
        ORDER BY view_count DESC
        LIMIT @Limit
        """;

    #endregion

    #region 分类相关

    /// <summary>
    /// 创建分类
    /// </summary>
    public const string CreateCategory = """
        INSERT INTO categories (id, name, description, icon, sort_order, article_count, created_at)
        VALUES (@Id, @Name, @Description, @Icon, @SortOrder, @ArticleCount, @CreatedAt)
        """;

    /// <summary>
    /// 获取所有分类
    /// </summary>
    public const string GetAllCategories = """
        SELECT id, name, description, icon, sort_order, article_count, created_at
        FROM categories
        WHERE deleted_at IS NULL
        ORDER BY sort_order ASC
        """;

    /// <summary>
    /// 更新分类
    /// </summary>
    public const string UpdateCategory = """
        UPDATE categories
        SET name = @Name, description = @Description, icon = @Icon, sort_order = @SortOrder, updated_at = @UpdatedAt
        WHERE id = @Id AND deleted_at IS NULL
        """;

    /// <summary>
    /// 更新分类文章数
    /// </summary>
    public const string UpdateCategoryArticleCount = """
        UPDATE categories
        SET article_count = (SELECT COUNT(*) FROM articles WHERE category_id = @Id AND status = 1 AND deleted_at IS NULL)
        WHERE id = @Id
        """;

    /// <summary>
    /// 删除分类
    /// </summary>
    public const string SoftDeleteCategory = """
        UPDATE categories
        SET deleted_at = @DeletedAt
        WHERE id = @Id
        """;

    #endregion

    #region 标签相关

    /// <summary>
    /// 创建标签
    /// </summary>
    public const string CreateTag = """
        INSERT INTO tags (id, name, color, article_count, created_at)
        VALUES (@Id, @Name, @Color, @ArticleCount, @CreatedAt)
        ON CONFLICT (name) DO NOTHING
        """;

    /// <summary>
    /// 获取所有标签
    /// </summary>
    public const string GetAllTags = """
        SELECT id, name, color, article_count, created_at
        FROM tags
        ORDER BY article_count DESC
        """;

    /// <summary>
    /// 获取文章的标签
    /// </summary>
    public const string GetArticleTags = """
        SELECT t.id, t.name, t.color, t.article_count, t.created_at
        FROM tags t
        INNER JOIN article_tags at ON t.id = at.tag_id
        WHERE at.article_id = @ArticleId
        """;

    /// <summary>
    /// 添加文章标签关联
    /// </summary>
    public const string AddArticleTag = """
        INSERT INTO article_tags (article_id, tag_id, created_at)
        VALUES (@ArticleId, @TagId, @CreatedAt)
        ON CONFLICT DO NOTHING
        """;

    /// <summary>
    /// 删除文章的所有标签关联
    /// </summary>
    public const string DeleteArticleTags = """
        DELETE FROM article_tags WHERE article_id = @ArticleId
        """;

    /// <summary>
    /// 更新标签文章数
    /// </summary>
    public const string UpdateTagArticleCount = """
        UPDATE tags
        SET article_count = (SELECT COUNT(*) FROM article_tags at 
                            INNER JOIN articles a ON at.article_id = a.id
                            WHERE at.tag_id = @Id AND a.status = 1 AND a.deleted_at IS NULL)
        WHERE id = @Id
        """;

    #endregion
}
