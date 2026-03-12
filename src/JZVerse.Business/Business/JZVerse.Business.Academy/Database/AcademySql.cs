namespace JZVerse.Business.Academy.Database;

/// <summary>
/// 在线教育 SQL 语句
/// </summary>
public static class AcademySql
{
    #region 课程相关

    public const string CreateCourse = """
        INSERT INTO courses (id, title, summary, description, cover_image, category_id, difficulty,
                            total_duration, chapter_count, lesson_count, instructor_id, instructor_name,
                            status, view_count, play_count, is_recommended, published_at, created_at)
        VALUES (@Id, @Title, @Summary, @Description, @CoverImage, @CategoryId, @Difficulty,
                @TotalDuration, @ChapterCount, @LessonCount, @InstructorId, @InstructorName,
                @Status, @ViewCount, @PlayCount, @IsRecommended, @PublishedAt, @CreatedAt)
        """;

    public const string GetCourseById = """
        SELECT id, title, summary, description, cover_image, category_id, difficulty,
               total_duration, chapter_count, lesson_count, instructor_id, instructor_name,
               status, view_count, play_count, is_recommended, published_at, created_at, updated_at
        FROM courses
        WHERE id = @Id AND deleted_at IS NULL
        """;

    public const string GetCourseListAdmin = """
        SELECT id, title, summary, cover_image, category_id, difficulty, total_duration,
               chapter_count, lesson_count, instructor_name, status, view_count, play_count,
               is_recommended, published_at, created_at
        FROM courses
        WHERE deleted_at IS NULL
          AND (@Status IS NULL OR status = @Status)
          AND (@CategoryId IS NULL OR category_id = @CategoryId)
          AND (@Difficulty IS NULL OR difficulty = @Difficulty)
          AND (@Keyword IS NULL OR title ILIKE '%' || @Keyword || '%')
        ORDER BY created_at DESC
        LIMIT @Limit OFFSET @Offset
        """;

    public const string GetCourseListPublic = """
        SELECT id, title, summary, cover_image, category_id, difficulty, total_duration,
               chapter_count, lesson_count, instructor_name, view_count, play_count, is_recommended, published_at
        FROM courses
        WHERE status = 1 AND deleted_at IS NULL
          AND (@CategoryId IS NULL OR category_id = @CategoryId)
          AND (@Difficulty IS NULL OR difficulty = @Difficulty)
          AND (@Keyword IS NULL OR title ILIKE '%' || @Keyword || '%')
        ORDER BY is_recommended DESC, published_at DESC
        LIMIT @Limit OFFSET @Offset
        """;

    public const string GetCourseCount = """
        SELECT COUNT(*) FROM courses
        WHERE deleted_at IS NULL
          AND (@Status IS NULL OR status = @Status)
          AND (@CategoryId IS NULL OR category_id = @CategoryId)
          AND (@Difficulty IS NULL OR difficulty = @Difficulty)
          AND (@Keyword IS NULL OR title ILIKE '%' || @Keyword || '%')
        """;

    public const string UpdateCourse = """
        UPDATE courses
        SET title = @Title, summary = @Summary, description = @Description, cover_image = @CoverImage,
            category_id = @CategoryId, difficulty = @Difficulty, is_recommended = @IsRecommended, updated_at = @UpdatedAt
        WHERE id = @Id AND deleted_at IS NULL
        """;

    public const string PublishCourse = """
        UPDATE courses
        SET status = 1, published_at = @PublishedAt, updated_at = @UpdatedAt
        WHERE id = @Id AND deleted_at IS NULL
        """;

    public const string OfflineCourse = """
        UPDATE courses SET status = 2, updated_at = @UpdatedAt WHERE id = @Id AND deleted_at IS NULL
        """;

    public const string SoftDeleteCourse = """
        UPDATE courses SET deleted_at = @DeletedAt WHERE id = @Id
        """;

    public const string IncrementViewCount = """
        UPDATE courses SET view_count = view_count + 1 WHERE id = @Id
        """;

    public const string GetRecommendedCourses = """
        SELECT id, title, summary, cover_image, category_id, difficulty, total_duration,
               instructor_name, view_count, play_count, published_at
        FROM courses
        WHERE status = 1 AND is_recommended = TRUE AND deleted_at IS NULL
        ORDER BY published_at DESC
        LIMIT @Limit
        """;

    public const string GetHotCourses = """
        SELECT id, title, summary, cover_image, category_id, difficulty, total_duration,
               instructor_name, view_count, play_count, published_at
        FROM courses
        WHERE status = 1 AND deleted_at IS NULL
        ORDER BY play_count DESC
        LIMIT @Limit
        """;

    #endregion

    #region 分类相关

    public const string GetAllCategories = """
        SELECT id, parent_id, name, description, icon, sort_order, course_count, created_at
        FROM course_categories
        WHERE deleted_at IS NULL
        ORDER BY sort_order ASC
        """;

    public const string CreateCategory = """
        INSERT INTO course_categories (id, parent_id, name, description, icon, sort_order, course_count, created_at)
        VALUES (@Id, @ParentId, @Name, @Description, @Icon, @SortOrder, @CourseCount, @CreatedAt)
        """;

    #endregion

    #region 章节相关

    public const string CreateChapter = """
        INSERT INTO chapters (id, course_id, title, sort_order, lesson_count, total_duration, created_at)
        VALUES (@Id, @CourseId, @Title, @SortOrder, @LessonCount, @TotalDuration, @CreatedAt)
        """;

    public const string GetChaptersByCourse = """
        SELECT id, course_id, title, sort_order, lesson_count, total_duration, created_at
        FROM chapters
        WHERE course_id = @CourseId
        ORDER BY sort_order ASC
        """;

    public const string UpdateChapter = """
        UPDATE chapters SET title = @Title, sort_order = @SortOrder, updated_at = @UpdatedAt WHERE id = @Id
        """;

    public const string DeleteChapter = """
        DELETE FROM chapters WHERE id = @Id
        """;

    #endregion

    #region 课时相关

    public const string CreateLesson = """
        INSERT INTO lessons (id, course_id, chapter_id, title, duration, video_file_id, video_url,
                            sort_order, is_free, play_count, created_at)
        VALUES (@Id, @CourseId, @ChapterId, @Title, @Duration, @VideoFileId, @VideoUrl,
                @SortOrder, @IsFree, @PlayCount, @CreatedAt)
        """;

    public const string GetLessonsByChapter = """
        SELECT id, course_id, chapter_id, title, duration, video_file_id, video_url,
               sort_order, is_free, play_count, created_at
        FROM lessons
        WHERE chapter_id = @ChapterId
        ORDER BY sort_order ASC
        """;

    public const string GetLessonsByCourse = """
        SELECT id, course_id, chapter_id, title, duration, video_file_id, video_url,
               sort_order, is_free, play_count, created_at
        FROM lessons
        WHERE course_id = @CourseId
        ORDER BY sort_order ASC
        """;

    public const string GetLessonById = """
        SELECT id, course_id, chapter_id, title, duration, video_file_id, video_url,
               sort_order, is_free, play_count, created_at
        FROM lessons
        WHERE id = @Id
        """;

    public const string IncrementLessonPlayCount = """
        UPDATE lessons SET play_count = play_count + 1 WHERE id = @Id
        """;

    public const string UpdateLesson = """
        UPDATE lessons
        SET title = @Title, duration = @Duration, video_file_id = @VideoFileId, video_url = @VideoUrl,
            sort_order = @SortOrder, is_free = @IsFree, updated_at = @UpdatedAt
        WHERE id = @Id
        """;

    public const string DeleteLesson = """
        DELETE FROM lessons WHERE id = @Id
        """;

    #endregion

    #region 资料相关

    public const string CreateMaterial = """
        INSERT INTO course_materials (id, course_id, lesson_id, title, file_id, file_type, download_count, created_at)
        VALUES (@Id, @CourseId, @LessonId, @Title, @FileId, @FileType, @DownloadCount, @CreatedAt)
        """;

    public const string GetMaterialsByCourse = """
        SELECT id, course_id, lesson_id, title, file_id, file_type, download_count, created_at
        FROM course_materials
        WHERE course_id = @CourseId
        ORDER BY created_at ASC
        """;

    public const string IncrementMaterialDownload = """
        UPDATE course_materials SET download_count = download_count + 1 WHERE id = @Id
        """;

    #endregion
}
