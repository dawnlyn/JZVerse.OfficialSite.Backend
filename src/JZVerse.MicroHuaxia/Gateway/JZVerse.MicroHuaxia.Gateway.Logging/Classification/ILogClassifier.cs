namespace JZVerse.MicroHuaxia.Gateway.Logging.Classification;

/// <summary>
/// 日志分类器接口
/// </summary>
public interface ILogClassifier
{
    /// <summary>
    /// 对日志条目进行分类
    /// </summary>
    /// <param name="entry">日志条目</param>
    /// <returns>分类路径（如 "GatewayHttp/Error/2026-02-06"）</returns>
    string Classify(Storage.LogEntry entry);
}
