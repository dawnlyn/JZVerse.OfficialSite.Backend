using System.IO.Compression;
using JZVerse.MicroHuaxia.Gateway.Logging.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Gateway.Logging.Storage.File;

/// <summary>
/// 日志文件轮转器
/// </summary>
public sealed class LogFileRotator
{
    private readonly FileLogStoreOptions _options;
    private readonly ILogger<LogFileRotator> _logger;
    private readonly object _rotateLock = new();

    /// <summary>
    /// 创建日志文件轮转器
    /// </summary>
    public LogFileRotator(
        IOptions<FileLogStoreOptions> options,
        ILogger<LogFileRotator> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 检查文件是否需要轮转
    /// </summary>
    public bool NeedsRotation(string filePath)
    {
        if (!_options.EnableRotation)
            return false;

        if (!System.IO.File.Exists(filePath))
            return false;

        var fileInfo = new FileInfo(filePath);

        return _options.RotationStrategy switch
        {
            RotationStrategy.Size => fileInfo.Length >= _options.MaxFileSizeBytes,
            RotationStrategy.Time => NeedsTimeRotation(fileInfo),
            RotationStrategy.Both => fileInfo.Length >= _options.MaxFileSizeBytes || NeedsTimeRotation(fileInfo),
            _ => false
        };
    }

    /// <summary>
    /// 执行文件轮转
    /// </summary>
    /// <returns>新文件路径</returns>
    public string? Rotate(string filePath)
    {
        if (!System.IO.File.Exists(filePath))
            return null;

        lock (_rotateLock)
        {
            try
            {
                var directory = Path.GetDirectoryName(filePath)!;
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var extension = Path.GetExtension(filePath);

                // 查找下一个可用的序号
                var sequence = 1;
                string rotatedPath;
                do
                {
                    rotatedPath = Path.Combine(directory, $"{fileName}.{sequence:D3}{extension}");
                    sequence++;
                } while (System.IO.File.Exists(rotatedPath) || System.IO.File.Exists(rotatedPath + ".gz"));

                // 重命名文件
                System.IO.File.Move(filePath, rotatedPath);

                _logger.LogDebug("日志文件已轮转: {OldPath} -> {NewPath}", filePath, rotatedPath);

                return rotatedPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "日志文件轮转失败: {FilePath}", filePath);
                return null;
            }
        }
    }

    private bool NeedsTimeRotation(FileInfo fileInfo)
    {
        var now = _options.UseUtcTime ? DateTimeOffset.UtcNow : DateTimeOffset.Now;
        var fileAge = now - fileInfo.CreationTimeUtc;
        return fileAge.TotalHours >= _options.RotationIntervalHours;
    }
}
