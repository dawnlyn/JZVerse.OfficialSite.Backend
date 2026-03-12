namespace JZVerse.Business.Central.File.Database;

/// <summary>
/// 文件实体
/// </summary>
public sealed record FileEntity
{
    /// <summary>
    /// 文件唯一标识
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 存储文件名（UUID 生成的唯一文件名）
    /// </summary>
    public string FileName { get; init; } = "";

    /// <summary>
    /// 原始文件名（用户上传时的文件名）
    /// </summary>
    public string OriginalName { get; init; } = "";

    /// <summary>
    /// 文件类型（image/document/video/audio/archive/other）
    /// </summary>
    public string FileType { get; init; } = "";

    /// <summary>
    /// MIME 类型
    /// </summary>
    public string MimeType { get; init; } = "";

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public long FileSize { get; init; }

    /// <summary>
    /// 存储路径
    /// </summary>
    public string StoragePath { get; init; } = "";

    /// <summary>
    /// 缩略图路径（图片文件时使用）
    /// </summary>
    public string? ThumbnailPath { get; init; }

    /// <summary>
    /// 所属模块（blog/academy/project 等）
    /// </summary>
    public string Module { get; init; } = "";

    /// <summary>
    /// 关联内容 ID
    /// </summary>
    public Guid? ContentId { get; init; }

    /// <summary>
    /// 文件状态
    /// </summary>
    public FileStatus Status { get; init; }

    /// <summary>
    /// 上传者 ID
    /// </summary>
    public Guid UploaderId { get; init; }

    /// <summary>
    /// 上传者名称（冗余字段，便于查询展示）
    /// </summary>
    public string? UploaderName { get; init; }

    /// <summary>
    /// 下载次数
    /// </summary>
    public int DownloadCount { get; init; }

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
/// 文件状态
/// </summary>
public enum FileStatus
{
    /// <summary>
    /// 正常
    /// </summary>
    Normal = 1,

    /// <summary>
    /// 禁用
    /// </summary>
    Disabled = 2,

    /// <summary>
    /// 已删除（回收站）
    /// </summary>
    Deleted = 3
}

/// <summary>
/// 文件上传日志实体
/// </summary>
public sealed record FileUploadLogEntity
{
    /// <summary>
    /// 日志唯一标识
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 文件 ID
    /// </summary>
    public Guid FileId { get; init; }

    /// <summary>
    /// 文件名
    /// </summary>
    public string FileName { get; init; } = "";

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public long FileSize { get; init; }

    /// <summary>
    /// 文件类型
    /// </summary>
    public string FileType { get; init; } = "";

    /// <summary>
    /// 上传者 ID
    /// </summary>
    public Guid UploaderId { get; init; }

    /// <summary>
    /// 上传者名称
    /// </summary>
    public string? UploaderName { get; init; }

    /// <summary>
    /// 上传者 IP 地址
    /// </summary>
    public string? UploaderIp { get; init; }

    /// <summary>
    /// 是否上传成功
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 失败时的错误信息
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// 文件下载日志实体
/// </summary>
public sealed record FileDownloadLogEntity
{
    /// <summary>
    /// 日志唯一标识
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 文件 ID
    /// </summary>
    public Guid FileId { get; init; }

    /// <summary>
    /// 文件名
    /// </summary>
    public string FileName { get; init; } = "";

    /// <summary>
    /// 下载者 ID（匿名下载时为 null）
    /// </summary>
    public Guid? DownloaderId { get; init; }

    /// <summary>
    /// 下载者名称
    /// </summary>
    public string? DownloaderName { get; init; }

    /// <summary>
    /// 下载者 IP 地址
    /// </summary>
    public string? DownloaderIp { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// 存储容量统计实体
/// </summary>
public sealed record StorageStatEntity
{
    /// <summary>
    /// 模块名称
    /// </summary>
    public string Module { get; init; } = "";

    /// <summary>
    /// 文件数量
    /// </summary>
    public int FileCount { get; init; }

    /// <summary>
    /// 总大小（字节）
    /// </summary>
    public long TotalSize { get; init; }
}

/// <summary>
/// 文件格式配置
/// </summary>
public static class FileFormatConfig
{
    /// <summary>
    /// 允许的图片格式
    /// </summary>
    public static readonly string[] ImageFormats = [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg"];

    /// <summary>
    /// 允许的文档格式
    /// </summary>
    public static readonly string[] DocumentFormats = [".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".pdf", ".txt", ".md"];

    /// <summary>
    /// 允许的视频格式
    /// </summary>
    public static readonly string[] VideoFormats = [".mp4", ".flv", ".avi", ".mov", ".wmv", ".mkv", ".webm"];

    /// <summary>
    /// 允许的音频格式
    /// </summary>
    public static readonly string[] AudioFormats = [".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma"];

    /// <summary>
    /// 允许的压缩包格式
    /// </summary>
    public static readonly string[] ArchiveFormats = [".zip", ".rar", ".7z", ".tar", ".gz"];

    /// <summary>
    /// 所有允许的格式
    /// </summary>
    public static readonly string[] AllowedFormats =
    [
        ..ImageFormats,
        ..DocumentFormats,
        ..VideoFormats,
        ..AudioFormats,
        ..ArchiveFormats
    ];

    /// <summary>
    /// 文件大小限制（字节）
    /// </summary>
    public static class SizeLimits
    {
        /// <summary>
        /// 图片最大 50MB
        /// </summary>
        public const long Image = 50 * 1024 * 1024;

        /// <summary>
        /// 文档最大 100MB
        /// </summary>
        public const long Document = 100 * 1024 * 1024;

        /// <summary>
        /// 视频最大 2GB
        /// </summary>
        public const long Video = 2L * 1024 * 1024 * 1024;

        /// <summary>
        /// 音频最大 200MB
        /// </summary>
        public const long Audio = 200 * 1024 * 1024;

        /// <summary>
        /// 压缩包最大 500MB
        /// </summary>
        public const long Archive = 500 * 1024 * 1024;
    }

    /// <summary>
    /// 获取文件类型
    /// </summary>
    public static string GetFileType(string extension)
    {
        extension = extension.ToLower();
        if (ImageFormats.Contains(extension)) return "image";
        if (DocumentFormats.Contains(extension)) return "document";
        if (VideoFormats.Contains(extension)) return "video";
        if (AudioFormats.Contains(extension)) return "audio";
        if (ArchiveFormats.Contains(extension)) return "archive";
        return "other";
    }

    /// <summary>
    /// 获取文件大小限制
    /// </summary>
    public static long GetSizeLimit(string fileType) => fileType switch
    {
        "image" => SizeLimits.Image,
        "document" => SizeLimits.Document,
        "video" => SizeLimits.Video,
        "audio" => SizeLimits.Audio,
        "archive" => SizeLimits.Archive,
        _ => SizeLimits.Document
    };
}
