using JZVerse.MicroHuaxia.Gateway.Logging.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Gateway.Logging.Storage.File;

/// <summary>
/// 日志文件保留策略
/// </summary>
public sealed class LogFileRetentionPolicy
{
    private readonly FileLogStoreOptions _options;
    private readonly ILogger<LogFileRetentionPolicy> _logger;

    /// <summary>
    /// 创建日志文件保留策略
    /// </summary>
    public LogFileRetentionPolicy(
        IOptions<FileLogStoreOptions> options,
        ILogger<LogFileRetentionPolicy> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 执行保留策略（删除过期文件）
    /// </summary>
    public Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        var basePath = _options.BasePath;
        if (!Directory.Exists(basePath))
            return Task.CompletedTask;

        var deletedCount = 0;
        long freedBytes = 0;

        // 按时间删除过期文件
        var cutoffTime = (_options.UseUtcTime ? DateTimeOffset.UtcNow : DateTimeOffset.Now)
            .AddDays(-_options.RetentionDays);

        var allFiles = GetAllLogFiles(basePath);

        foreach (var file in allFiles)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastWriteTimeUtc <= cutoffTime.UtcDateTime)
                {
                    var size = fileInfo.Length;
                    System.IO.File.Delete(file);
                    deletedCount++;
                    freedBytes += size;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "删除过期日志文件失败: {FilePath}", file);
            }
        }

        // 按总大小限制删除
        freedBytes += ApplySizeLimit(basePath, cancellationToken, ref deletedCount);

        if (deletedCount > 0)
        {
            _logger.LogInformation("已删除 {Count} 个过期日志文件，释放 {Size:F2} MB",
                deletedCount, freedBytes / (1024.0 * 1024.0));
        }

        // 清理空目录
        CleanupEmptyDirectories(basePath);

        return Task.CompletedTask;
    }

    private long ApplySizeLimit(string basePath, CancellationToken cancellationToken, ref int deletedCount)
    {
        long freedBytes = 0;

        // 获取所有文件并按修改时间排序（最旧的在前）
        var allFiles = GetAllLogFiles(basePath)
            .Select(f => new FileInfo(f))
            .OrderBy(f => f.LastWriteTimeUtc)
            .ToList();

        var totalSize = allFiles.Sum(f => f.Length);

        // 如果超过限制，从最旧的文件开始删除
        while (totalSize > _options.MaxTotalSizeBytes && allFiles.Count > 0)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var oldestFile = allFiles[0];
            allFiles.RemoveAt(0);

            try
            {
                var size = oldestFile.Length;
                oldestFile.Delete();
                totalSize -= size;
                freedBytes += size;
                deletedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "删除日志文件失败: {FilePath}", oldestFile.FullName);
            }
        }

        return freedBytes;
    }

    /// <summary>
    /// 获取存储统计信息
    /// </summary>
    public (long totalSize, int fileCount, int compressedCount) GetStorageStats()
    {
        var basePath = _options.BasePath;
        if (!Directory.Exists(basePath))
            return (0, 0, 0);

        var allFiles = GetAllLogFiles(basePath).ToList();
        var totalSize = allFiles.Sum(f => new FileInfo(f).Length);
        var compressedCount = allFiles.Count(f => f.EndsWith(".gz"));

        return (totalSize, allFiles.Count, compressedCount);
    }

    private static IEnumerable<string> GetAllLogFiles(string basePath)
    {
        return Directory.EnumerateFiles(basePath, "*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".log") || f.EndsWith(".log.gz"));
    }

    private void CleanupEmptyDirectories(string basePath)
    {
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(basePath, "*", SearchOption.AllDirectories)
                .OrderByDescending(d => d.Length)) // 先删除深层目录
            {
                if (!Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    Directory.Delete(dir);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "清理空目录时出错");
        }
    }
}
