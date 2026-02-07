using JZVerse.MicroHuaxia.Gateway.Logging.Configuration;
using JZVerse.MicroHuaxia.Gateway.Logging.Storage;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Gateway.Logging.Classification;

/// <summary>
/// 混合分类器（服务 + 级别 + 时间）
/// </summary>
public sealed class CompositeClassifier : ILogClassifier
{
    private readonly GatewayLoggingOptions _options;

    /// <summary>
    /// 创建混合分类器
    /// </summary>
    public CompositeClassifier(IOptions<GatewayLoggingOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    public string Classify(LogEntry entry)
    {
        return _options.ClassificationStrategy switch
        {
            ClassificationStrategy.Service => ClassifyByService(entry),
            ClassificationStrategy.Level => ClassifyByLevel(entry),
            ClassificationStrategy.Time => ClassifyByTime(entry),
            ClassificationStrategy.Composite => ClassifyComposite(entry),
            _ => "Unknown"
        };
    }

    private static string ClassifyByService(LogEntry entry)
    {
        var serviceName = SanitizePath(entry.ServiceName ?? "Unknown");
        return serviceName;
    }

    private static string ClassifyByLevel(LogEntry entry)
    {
        return entry.Level.ToString();
    }

    private string ClassifyByTime(LogEntry entry)
    {
        return _options.TimeClassificationGranularity switch
        {
            TimeClassificationGranularity.Hour => entry.Timestamp.ToString("yyyy/MM/dd/HH"),
            TimeClassificationGranularity.Day => entry.Timestamp.ToString("yyyy/MM/dd"),
            _ => entry.Timestamp.ToString("yyyy/MM/dd")
        };
    }

    private string ClassifyComposite(LogEntry entry)
    {
        var service = SanitizePath(entry.ServiceName ?? "Unknown");
        var level = entry.Level.ToString();
        var time = _options.TimeClassificationGranularity switch
        {
            TimeClassificationGranularity.Hour => entry.Timestamp.ToString("yyyy-MM-dd-HH"),
            TimeClassificationGranularity.Day => entry.Timestamp.ToString("yyyy-MM-dd"),
            _ => entry.Timestamp.ToString("yyyy-MM-dd")
        };

        return $"{service}/{level}/{time}";
    }

    private static string SanitizePath(string path)
    {
        var invalidChars = Path.GetInvalidPathChars().Concat(Path.GetInvalidFileNameChars()).Distinct();
        return string.Concat(path.Where(c => !invalidChars.Contains(c)));
    }
}
