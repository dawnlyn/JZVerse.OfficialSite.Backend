using JZVerse.Business.Abstractions.Models;
using JZVerse.Business.Central.File.Arguments;
using JZVerse.Business.Central.File.Database;
using JZVerse.Business.Central.File.Results;
using JZVerse.MicroHuaxia.DataAccess.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace JZVerse.Business.Central.File.Services;

/// <summary>
/// 文件存储配置
/// </summary>
public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>
    /// 存储根路径
    /// </summary>
    public string RootPath { get; set; } = "uploads";

    /// <summary>
    /// 访问基础 URL
    /// </summary>
    public string BaseUrl { get; set; } = "/files";

    /// <summary>
    /// 总容量限制（字节），默认 100GB
    /// </summary>
    public long TotalCapacity { get; set; } = 100L * 1024 * 1024 * 1024;

    /// <summary>
    /// 回收站自动清理天数，默认 7 天
    /// </summary>
    public int RecycleBinRetentionDays { get; set; } = 7;
}

/// <summary>
/// 文件服务
/// </summary>
public sealed class FileService
{
    private readonly IDbExecutor _db;
    private readonly ISnowflakeIdGenerator _idGenerator;
    private readonly FileStorageOptions _options;

    public FileService(IDbExecutor db, ISnowflakeIdGenerator idGenerator, IOptions<FileStorageOptions> options)
    {
        _db = db;
        _idGenerator = idGenerator;
        _options = options.Value;
    }

    /// <summary>
    /// 上传文件
    /// </summary>
    public async Task<ResultUploadFile> UploadAsync(IFormFile file, string module, Guid uploaderId, string? uploaderName, Guid? contentId = null)
    {
        // 校验文件格式
        var extension = Path.GetExtension(file.FileName).ToLower();
        if (!FileFormatConfig.AllowedFormats.Contains(extension))
            throw new ValidationException($"不支持的文件格式: {extension}");

        // 获取文件类型
        var fileType = FileFormatConfig.GetFileType(extension);

        // 校验文件大小
        var sizeLimit = FileFormatConfig.GetSizeLimit(fileType);
        if (file.Length > sizeLimit)
            throw new ValidationException($"文件大小超出限制，最大允许 {FormatFileSize(sizeLimit)}");

        // 生成文件名和路径
        var fileId = _idGenerator.GenerateGuid();
        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{fileId}{extension}";
        var relativePath = Path.Combine(module, DateTime.UtcNow.ToString("yyyy/MM"), fileName);
        var absolutePath = Path.Combine(_options.RootPath, relativePath);

        // 确保目录存在
        var directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        // 保存文件
        await using (var stream = new FileStream(absolutePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // 创建数据库记录
        var entity = new FileEntity
        {
            Id = fileId,
            FileName = fileName,
            OriginalName = file.FileName,
            FileType = fileType,
            MimeType = file.ContentType,
            FileSize = file.Length,
            StoragePath = relativePath,
            Module = module,
            ContentId = contentId,
            Status = FileStatus.Normal,
            UploaderId = uploaderId,
            UploaderName = uploaderName,
            DownloadCount = 0,
            CreatedAt = DateTime.UtcNow
        };

        await _db.ExecuteAsync(FileSql.CreateFile, entity);

        // 记录上传日志
        await _db.ExecuteAsync(FileSql.CreateUploadLog, new FileUploadLogEntity
        {
            Id = _idGenerator.GenerateGuid(),
            FileId = fileId,
            FileName = file.FileName,
            FileSize = file.Length,
            FileType = fileType,
            UploaderId = uploaderId,
            UploaderName = uploaderName,
            IsSuccess = true,
            CreatedAt = DateTime.UtcNow
        });

        return new ResultUploadFile
        {
            FileId = fileId,
            FileName = fileName,
            OriginalName = file.FileName,
            FileType = fileType,
            FileSize = file.Length,
            PreviewUrl = $"{_options.BaseUrl}/preview/{fileId}",
            DownloadUrl = $"{_options.BaseUrl}/download/{fileId}"
        };
    }

    /// <summary>
    /// 获取文件信息
    /// </summary>
    public async Task<ResultFileInfo?> GetByIdAsync(Guid id)
    {
        var entity = await _db.QueryFirstOrDefaultAsync<FileEntity>(FileSql.GetFileById, new { Id = id });
        return entity is null ? null : MapToFileInfo(entity);
    }

    /// <summary>
    /// 获取文件流（用于预览/下载）
    /// </summary>
    public async Task<(Stream stream, string contentType, string fileName)?> GetFileStreamAsync(Guid id, bool isDownload = false)
    {
        var entity = await _db.QueryFirstOrDefaultAsync<FileEntity>(FileSql.GetFileById, new { Id = id });
        if (entity is null || entity.Status != FileStatus.Normal)
            return null;

        var absolutePath = Path.Combine(_options.RootPath, entity.StoragePath);
        if (!System.IO.File.Exists(absolutePath))
            return null;

        if (isDownload)
        {
            await _db.ExecuteAsync(FileSql.IncrementDownloadCount, new { Id = id });
        }

        var stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read);
        return (stream, entity.MimeType, entity.OriginalName);
    }

    /// <summary>
    /// 获取文件列表
    /// </summary>
    public async Task<PagedResult<ResultFileInfo>> GetListAsync(ArgQueryFiles arg)
    {
        var offset = (arg.PageIndex - 1) * arg.PageSize;

        var files = await _db.QueryAsync<FileEntity>(FileSql.GetFileList, new
        {
            arg.Module,
            arg.FileType,
            arg.Status,
            arg.Keyword,
            arg.StartTime,
            arg.EndTime,
            Limit = arg.PageSize,
            Offset = offset
        });

        var total = await _db.ExecuteScalarAsync<int>(FileSql.GetFileCount, new
        {
            arg.Module,
            arg.FileType,
            arg.Status,
            arg.Keyword,
            arg.StartTime,
            arg.EndTime
        });

        return new PagedResult<ResultFileInfo>
        {
            Items = files.Select(MapToFileInfo).ToList(),
            TotalCount = total,
            PageIndex = arg.PageIndex,
            PageSize = arg.PageSize
        };
    }

    /// <summary>
    /// 获取内容关联的文件列表
    /// </summary>
    public async Task<IReadOnlyList<ResultFileInfo>> GetByContentAsync(string module, Guid contentId)
    {
        var files = await _db.QueryAsync<FileEntity>(FileSql.GetFilesByContent, new { Module = module, ContentId = contentId });
        return files.Select(MapToFileInfo).ToList();
    }

    /// <summary>
    /// 禁用文件
    /// </summary>
    public async Task<bool> DisableAsync(Guid id)
    {
        var affected = await _db.ExecuteAsync(FileSql.UpdateFileStatus, new
        {
            Id = id,
            Status = FileStatus.Disabled,
            UpdatedAt = DateTime.UtcNow
        });
        return affected > 0;
    }

    /// <summary>
    /// 启用文件
    /// </summary>
    public async Task<bool> EnableAsync(Guid id)
    {
        var affected = await _db.ExecuteAsync(FileSql.UpdateFileStatus, new
        {
            Id = id,
            Status = FileStatus.Normal,
            UpdatedAt = DateTime.UtcNow
        });
        return affected > 0;
    }

    /// <summary>
    /// 删除文件（移到回收站）
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id)
    {
        var affected = await _db.ExecuteAsync(FileSql.SoftDeleteFile, new
        {
            Id = id,
            DeletedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        return affected > 0;
    }

    /// <summary>
    /// 批量删除文件
    /// </summary>
    public async Task<int> BatchDeleteAsync(Guid[] ids)
    {
        var affected = await _db.ExecuteAsync(FileSql.BatchUpdateFileStatus, new
        {
            Ids = ids,
            Status = FileStatus.Deleted,
            UpdatedAt = DateTime.UtcNow
        });
        return affected;
    }

    /// <summary>
    /// 恢复文件
    /// </summary>
    public async Task<bool> RestoreAsync(Guid id)
    {
        var affected = await _db.ExecuteAsync(FileSql.RestoreFile, new
        {
            Id = id,
            UpdatedAt = DateTime.UtcNow
        });
        return affected > 0;
    }

    /// <summary>
    /// 关联文件到内容
    /// </summary>
    public async Task<bool> LinkToContentAsync(Guid fileId, Guid contentId)
    {
        var affected = await _db.ExecuteAsync(FileSql.LinkFileToContent, new
        {
            Id = fileId,
            ContentId = contentId,
            UpdatedAt = DateTime.UtcNow
        });
        return affected > 0;
    }

    /// <summary>
    /// 获取存储统计
    /// </summary>
    public async Task<ResultStorageStats> GetStorageStatsAsync()
    {
        var totalStats = await _db.QueryFirstOrDefaultAsync<dynamic>(FileSql.GetTotalStorageStats);
        var moduleStats = await _db.QueryAsync<StorageStatEntity>(FileSql.GetStorageStats);

        var usedSize = (long)(totalStats?.total_size ?? 0);
        var fileCount = (int)(totalStats?.file_count ?? 0);

        return new ResultStorageStats
        {
            TotalSize = _options.TotalCapacity,
            UsedSize = usedSize,
            RemainingSize = _options.TotalCapacity - usedSize,
            TotalFileCount = fileCount,
            ModuleStats = moduleStats.Select(m => new ResultModuleStorageStats
            {
                Module = m.Module,
                FileCount = m.FileCount,
                TotalSize = m.TotalSize,
                Percentage = usedSize > 0 ? (double)m.TotalSize / usedSize * 100 : 0
            }).ToList()
        };
    }

    /// <summary>
    /// 获取回收站文件列表
    /// </summary>
    public async Task<PagedResult<ResultDeletedFile>> GetDeletedFilesAsync(int pageIndex, int pageSize)
    {
        var offset = (pageIndex - 1) * pageSize;

        var files = await _db.QueryAsync<FileEntity>(FileSql.GetDeletedFileList, new { Limit = pageSize, Offset = offset });
        var total = await _db.ExecuteScalarAsync<int>(FileSql.GetDeletedFileCount);

        return new PagedResult<ResultDeletedFile>
        {
            Items = files.Select(f => new ResultDeletedFile
            {
                Id = f.Id,
                OriginalName = f.OriginalName,
                FileType = f.FileType,
                FileSize = f.FileSize,
                Module = f.Module,
                UploaderName = f.UploaderName,
                CreatedAt = f.CreatedAt,
                DeletedAt = f.DeletedAt
            }).ToList(),
            TotalCount = total,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// 获取上传日志
    /// </summary>
    public async Task<PagedResult<ResultUploadLog>> GetUploadLogsAsync(ArgQueryUploadLogs arg)
    {
        var offset = (arg.PageIndex - 1) * arg.PageSize;

        var logs = await _db.QueryAsync<FileUploadLogEntity>(FileSql.GetUploadLogList, new
        {
            arg.StartTime,
            arg.EndTime,
            arg.IsSuccess,
            Limit = arg.PageSize,
            Offset = offset
        });

        var total = await _db.ExecuteScalarAsync<int>(FileSql.GetUploadLogCount, new
        {
            arg.StartTime,
            arg.EndTime,
            arg.IsSuccess
        });

        return new PagedResult<ResultUploadLog>
        {
            Items = logs.Select(l => new ResultUploadLog
            {
                Id = l.Id,
                FileId = l.FileId,
                FileName = l.FileName,
                FileSize = l.FileSize,
                FileType = l.FileType,
                UploaderName = l.UploaderName,
                UploaderIp = l.UploaderIp,
                IsSuccess = l.IsSuccess,
                ErrorMessage = l.ErrorMessage,
                CreatedAt = l.CreatedAt
            }).ToList(),
            TotalCount = total,
            PageIndex = arg.PageIndex,
            PageSize = arg.PageSize
        };
    }

    private ResultFileInfo MapToFileInfo(FileEntity entity) => new()
    {
        Id = entity.Id,
        FileName = entity.FileName,
        OriginalName = entity.OriginalName,
        FileType = entity.FileType,
        MimeType = entity.MimeType,
        FileSize = entity.FileSize,
        ThumbnailUrl = entity.ThumbnailPath is not null ? $"{_options.BaseUrl}/thumbnail/{entity.Id}" : null,
        PreviewUrl = $"{_options.BaseUrl}/preview/{entity.Id}",
        DownloadUrl = $"{_options.BaseUrl}/download/{entity.Id}",
        Module = entity.Module,
        ContentId = entity.ContentId,
        Status = entity.Status,
        UploaderName = entity.UploaderName,
        DownloadCount = entity.DownloadCount,
        CreatedAt = entity.CreatedAt
    };

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}
