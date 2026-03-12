using FileOptions = JZVerse.MicroHuaxia.Observability.Core.Configuration.FileOptions;

namespace JZVerse.MicroHuaxia.Observability.Core.FileLogging;

/// <summary>
/// 日志文件轮换和清理管理器 — 按保留天数和总大小清理旧日志
/// </summary>
public sealed class FileLogRotator
{
    private readonly FileOptions _options;
    private readonly string _serviceName;
    private readonly Timer _cleanupTimer;

    public FileLogRotator(FileOptions options, string serviceName)
    {
        _options = options;
        _serviceName = serviceName;

        // 启动时立即清理一次，之后每小时清理
        _cleanupTimer = new Timer(
            _ => Cleanup(),
            null,
            TimeSpan.Zero,
            TimeSpan.FromHours(1));
    }

    /// <summary>
    /// 执行清理：按保留天数和总大小删除旧日志文件
    /// </summary>
    public void Cleanup()
    {
        try
        {
            var dir = GetLogDirectory();
            if (!Directory.Exists(dir)) return;

            var logFiles = Directory.GetFiles(dir, "*.log")
                .Select(f => new FileInfo(f))
                .OrderBy(f => f.CreationTime)
                .ToList();

            if (logFiles.Count == 0) return;

            // 按保留天数清理
            if (_options.RetainDays > 0)
            {
                var cutoff = DateTime.Now.AddDays(-_options.RetainDays);
                foreach (var file in logFiles.Where(f => f.CreationTime < cutoff).ToList())
                {
                    TryDelete(file);
                    logFiles.Remove(file);
                }
            }

            // 按总大小清理
            if (_options.MaxTotalSizeMB > 0)
            {
                var maxBytes = (long)_options.MaxTotalSizeMB * 1024 * 1024;
                var totalSize = logFiles.Sum(f => f.Length);

                while (totalSize > maxBytes && logFiles.Count > 1)
                {
                    var oldest = logFiles[0];
                    totalSize -= oldest.Length;
                    TryDelete(oldest);
                    logFiles.RemoveAt(0);
                }
            }
        }
        catch
        {
            // 清理失败不影响正常运行
        }
    }

    private static void TryDelete(FileInfo file)
    {
        try
        {
            file.Delete();
        }
        catch
        {
            // 文件可能正在使用，忽略
        }
    }

    private string GetLogDirectory()
    {
        var dir = _options.Directory;
        if (!Path.IsPathRooted(dir))
        {
            dir = Path.Combine(AppContext.BaseDirectory, dir);
        }
        return dir;
    }

    public void Stop()
    {
        _cleanupTimer.Dispose();
    }
}
