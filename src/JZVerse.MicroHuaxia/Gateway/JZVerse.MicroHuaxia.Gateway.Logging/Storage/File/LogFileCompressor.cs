using System.IO.Compression;
using JZVerse.MicroHuaxia.Gateway.Logging.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Gateway.Logging.Storage.File;

/// <summary>
/// 日志文件压缩器
/// </summary>
public sealed class LogFileCompressor
{
    private readonly FileLogStoreOptions _options;
    private readonly ILogger<LogFileCompressor> _logger;

    /// <summary>
    /// 创建日志文件压缩器
    /// </summary>
    public LogFileCompressor(
        IOptions<FileLogStoreOptions> options,
        ILogger<LogFileCompressor> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 压缩符合条件的文件
    /// </summary>
    public async Task CompressEligibleFilesAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.EnableCompression || _options.CompressionFormat == CompressionFormat.None)
            return;

        var basePath = _options.BasePath;
        if (!Directory.Exists(basePath))
            return;

        var cutoffTime = (_options.UseUtcTime ? DateTimeOffset.UtcNow : DateTimeOffset.Now)
            .AddDays(-_options.CompressAfterDays);

        var logFiles = Directory.EnumerateFiles(basePath, "*.log", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(".gz"))
            .ToList();

        foreach (var file in logFiles)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastWriteTimeUtc <= cutoffTime.UtcDateTime)
                {
                    await CompressFileAsync(file, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "压缩文件失败: {FilePath}", file);
            }
        }
    }

    /// <summary>
    /// 压缩单个文件
    /// </summary>
    public async Task<string?> CompressFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!System.IO.File.Exists(filePath))
            return null;

        var compressedPath = filePath + ".gz";

        try
        {
            await using var sourceStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            await using var destinationStream = new FileStream(compressedPath, FileMode.Create, FileAccess.Write);
            await using var gzipStream = new GZipStream(destinationStream, CompressionLevel.Optimal);

            await sourceStream.CopyToAsync(gzipStream, cancellationToken);
            await gzipStream.FlushAsync(cancellationToken);

            // 删除原文件
            System.IO.File.Delete(filePath);

            var originalSize = sourceStream.Length;
            var compressedSize = new FileInfo(compressedPath).Length;
            var ratio = originalSize > 0 ? (1.0 - (double)compressedSize / originalSize) * 100 : 0;

            _logger.LogDebug("文件已压缩: {FilePath} ({Ratio:F1}% 压缩率)", filePath, ratio);

            return compressedPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "压缩文件失败: {FilePath}", filePath);

            // 清理可能存在的不完整压缩文件
            if (System.IO.File.Exists(compressedPath))
            {
                try { System.IO.File.Delete(compressedPath); } catch { }
            }

            return null;
        }
    }

    /// <summary>
    /// 解压文件
    /// </summary>
    public async Task<string?> DecompressFileAsync(string compressedPath, CancellationToken cancellationToken = default)
    {
        if (!compressedPath.EndsWith(".gz") || !System.IO.File.Exists(compressedPath))
            return null;

        var decompressedPath = compressedPath[..^3]; // 移除 .gz 后缀

        try
        {
            await using var sourceStream = new FileStream(compressedPath, FileMode.Open, FileAccess.Read);
            await using var gzipStream = new GZipStream(sourceStream, CompressionMode.Decompress);
            await using var destinationStream = new FileStream(decompressedPath, FileMode.Create, FileAccess.Write);

            await gzipStream.CopyToAsync(destinationStream, cancellationToken);

            _logger.LogDebug("文件已解压: {FilePath}", compressedPath);

            return decompressedPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解压文件失败: {FilePath}", compressedPath);
            return null;
        }
    }
}
