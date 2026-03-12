namespace JZVerse.Business.Central.File.Database;

/// <summary>
/// 附件中心 SQL 语句
/// </summary>
public static class FileSql
{
    #region 文件相关

    /// <summary>
    /// 创建文件记录
    /// </summary>
    public const string CreateFile = """
        INSERT INTO files (id, file_name, original_name, file_type, mime_type, file_size, storage_path,
                          thumbnail_path, module, content_id, status, uploader_id, uploader_name, download_count, created_at)
        VALUES (@Id, @FileName, @OriginalName, @FileType, @MimeType, @FileSize, @StoragePath,
                @ThumbnailPath, @Module, @ContentId, @Status, @UploaderId, @UploaderName, @DownloadCount, @CreatedAt)
        """;

    /// <summary>
    /// 根据 ID 获取文件
    /// </summary>
    public const string GetFileById = """
        SELECT id, file_name, original_name, file_type, mime_type, file_size, storage_path,
               thumbnail_path, module, content_id, status, uploader_id, uploader_name, download_count, created_at, updated_at, deleted_at
        FROM files
        WHERE id = @Id
        """;

    /// <summary>
    /// 获取文件列表
    /// </summary>
    public const string GetFileList = """
        SELECT id, file_name, original_name, file_type, mime_type, file_size, storage_path,
               thumbnail_path, module, content_id, status, uploader_id, uploader_name, download_count, created_at, updated_at
        FROM files
        WHERE status != 3
          AND (@Module IS NULL OR module = @Module)
          AND (@FileType IS NULL OR file_type = @FileType)
          AND (@Status IS NULL OR status = @Status)
          AND (@StartTime IS NULL OR created_at >= @StartTime)
          AND (@EndTime IS NULL OR created_at <= @EndTime)
          AND (@Keyword IS NULL OR original_name ILIKE '%' || @Keyword || '%')
        ORDER BY created_at DESC
        LIMIT @Limit OFFSET @Offset
        """;

    /// <summary>
    /// 获取文件总数
    /// </summary>
    public const string GetFileCount = """
        SELECT COUNT(*) FROM files
        WHERE status != 3
          AND (@Module IS NULL OR module = @Module)
          AND (@FileType IS NULL OR file_type = @FileType)
          AND (@Status IS NULL OR status = @Status)
          AND (@StartTime IS NULL OR created_at >= @StartTime)
          AND (@EndTime IS NULL OR created_at <= @EndTime)
          AND (@Keyword IS NULL OR original_name ILIKE '%' || @Keyword || '%')
        """;

    /// <summary>
    /// 根据内容获取关联文件
    /// </summary>
    public const string GetFilesByContent = """
        SELECT id, file_name, original_name, file_type, mime_type, file_size, storage_path,
               thumbnail_path, module, content_id, status, uploader_id, uploader_name, download_count, created_at
        FROM files
        WHERE module = @Module AND content_id = @ContentId AND status = 1
        ORDER BY created_at ASC
        """;

    /// <summary>
    /// 更新文件状态
    /// </summary>
    public const string UpdateFileStatus = """
        UPDATE files
        SET status = @Status, updated_at = @UpdatedAt
        WHERE id = @Id
        """;

    /// <summary>
    /// 批量更新文件状态
    /// </summary>
    public const string BatchUpdateFileStatus = """
        UPDATE files
        SET status = @Status, updated_at = @UpdatedAt
        WHERE id = ANY(@Ids)
        """;

    /// <summary>
    /// 软删除文件
    /// </summary>
    public const string SoftDeleteFile = """
        UPDATE files
        SET status = 3, deleted_at = @DeletedAt, updated_at = @UpdatedAt
        WHERE id = @Id
        """;

    /// <summary>
    /// 恢复文件
    /// </summary>
    public const string RestoreFile = """
        UPDATE files
        SET status = 1, deleted_at = NULL, updated_at = @UpdatedAt
        WHERE id = @Id AND status = 3
        """;

    /// <summary>
    /// 彻底删除文件（清空回收站）
    /// </summary>
    public const string PermanentDeleteFile = """
        DELETE FROM files WHERE id = @Id AND status = 3
        """;

    /// <summary>
    /// 清理过期的回收站文件
    /// </summary>
    public const string CleanExpiredDeletedFiles = """
        DELETE FROM files WHERE status = 3 AND deleted_at < @ExpireTime
        RETURNING id, storage_path, thumbnail_path
        """;

    /// <summary>
    /// 更新下载次数
    /// </summary>
    public const string IncrementDownloadCount = """
        UPDATE files
        SET download_count = download_count + 1
        WHERE id = @Id
        """;

    /// <summary>
    /// 关联文件到内容
    /// </summary>
    public const string LinkFileToContent = """
        UPDATE files
        SET content_id = @ContentId, updated_at = @UpdatedAt
        WHERE id = @Id
        """;

    /// <summary>
    /// 获取回收站文件列表
    /// </summary>
    public const string GetDeletedFileList = """
        SELECT id, file_name, original_name, file_type, mime_type, file_size, storage_path,
               thumbnail_path, module, content_id, status, uploader_id, uploader_name, download_count, created_at, deleted_at
        FROM files
        WHERE status = 3
        ORDER BY deleted_at DESC
        LIMIT @Limit OFFSET @Offset
        """;

    /// <summary>
    /// 获取回收站文件总数
    /// </summary>
    public const string GetDeletedFileCount = """
        SELECT COUNT(*) FROM files WHERE status = 3
        """;

    #endregion

    #region 日志相关

    /// <summary>
    /// 创建上传日志
    /// </summary>
    public const string CreateUploadLog = """
        INSERT INTO file_upload_logs (id, file_id, file_name, file_size, file_type, uploader_id, uploader_name, uploader_ip, is_success, error_message, created_at)
        VALUES (@Id, @FileId, @FileName, @FileSize, @FileType, @UploaderId, @UploaderName, @UploaderIp, @IsSuccess, @ErrorMessage, @CreatedAt)
        """;

    /// <summary>
    /// 创建下载日志
    /// </summary>
    public const string CreateDownloadLog = """
        INSERT INTO file_download_logs (id, file_id, file_name, downloader_id, downloader_name, downloader_ip, created_at)
        VALUES (@Id, @FileId, @FileName, @DownloaderId, @DownloaderName, @DownloaderIp, @CreatedAt)
        """;

    /// <summary>
    /// 获取上传日志列表
    /// </summary>
    public const string GetUploadLogList = """
        SELECT id, file_id, file_name, file_size, file_type, uploader_id, uploader_name, uploader_ip, is_success, error_message, created_at
        FROM file_upload_logs
        WHERE (@StartTime IS NULL OR created_at >= @StartTime)
          AND (@EndTime IS NULL OR created_at <= @EndTime)
          AND (@IsSuccess IS NULL OR is_success = @IsSuccess)
        ORDER BY created_at DESC
        LIMIT @Limit OFFSET @Offset
        """;

    /// <summary>
    /// 获取上传日志总数
    /// </summary>
    public const string GetUploadLogCount = """
        SELECT COUNT(*) FROM file_upload_logs
        WHERE (@StartTime IS NULL OR created_at >= @StartTime)
          AND (@EndTime IS NULL OR created_at <= @EndTime)
          AND (@IsSuccess IS NULL OR is_success = @IsSuccess)
        """;

    #endregion

    #region 统计相关

    /// <summary>
    /// 获取存储统计
    /// </summary>
    public const string GetStorageStats = """
        SELECT module, COUNT(*) AS file_count, COALESCE(SUM(file_size), 0) AS total_size
        FROM files
        WHERE status = 1
        GROUP BY module
        """;

    /// <summary>
    /// 获取总存储统计
    /// </summary>
    public const string GetTotalStorageStats = """
        SELECT COUNT(*) AS file_count, COALESCE(SUM(file_size), 0) AS total_size
        FROM files
        WHERE status = 1
        """;

    #endregion
}
