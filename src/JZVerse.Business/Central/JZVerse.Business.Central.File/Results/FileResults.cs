using JZVerse.Business.Central.File.Database;

namespace JZVerse.Business.Central.File.Results;

/// <summary>
/// 文件信息结果
/// </summary>
public sealed record ResultFileInfo
{
    public Guid Id { get; init; }
    public string FileName { get; init; } = "";
    public string OriginalName { get; init; } = "";
    public string FileType { get; init; } = "";
    public string MimeType { get; init; } = "";
    public long FileSize { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string PreviewUrl { get; init; } = "";
    public string DownloadUrl { get; init; } = "";
    public string Module { get; init; } = "";
    public Guid? ContentId { get; init; }
    public FileStatus Status { get; init; }
    public string? UploaderName { get; init; }
    public int DownloadCount { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// 文件上传结果
/// </summary>
public sealed record ResultUploadFile
{
    public Guid FileId { get; init; }
    public string FileName { get; init; } = "";
    public string OriginalName { get; init; } = "";
    public string FileType { get; init; } = "";
    public long FileSize { get; init; }
    public string PreviewUrl { get; init; } = "";
    public string DownloadUrl { get; init; } = "";
    public string? ThumbnailUrl { get; init; }
}

/// <summary>
/// 存储统计结果
/// </summary>
public sealed record ResultStorageStats
{
    public long TotalSize { get; init; }
    public long UsedSize { get; init; }
    public long RemainingSize { get; init; }
    public int TotalFileCount { get; init; }
    public IReadOnlyList<ResultModuleStorageStats> ModuleStats { get; init; } = [];
}

/// <summary>
/// 模块存储统计结果
/// </summary>
public sealed record ResultModuleStorageStats
{
    public string Module { get; init; } = "";
    public int FileCount { get; init; }
    public long TotalSize { get; init; }
    public double Percentage { get; init; }
}

/// <summary>
/// 上传日志结果
/// </summary>
public sealed record ResultUploadLog
{
    public Guid Id { get; init; }
    public Guid FileId { get; init; }
    public string FileName { get; init; } = "";
    public long FileSize { get; init; }
    public string FileType { get; init; } = "";
    public string? UploaderName { get; init; }
    public string? UploaderIp { get; init; }
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// 回收站文件结果
/// </summary>
public sealed record ResultDeletedFile
{
    public Guid Id { get; init; }
    public string OriginalName { get; init; } = "";
    public string FileType { get; init; } = "";
    public long FileSize { get; init; }
    public string Module { get; init; } = "";
    public string? UploaderName { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? DeletedAt { get; init; }
}
