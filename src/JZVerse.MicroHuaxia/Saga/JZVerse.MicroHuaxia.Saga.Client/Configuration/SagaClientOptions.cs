namespace JZVerse.MicroHuaxia.Saga.Client.Configuration;

/// <summary>
/// Saga 客户端配置选项
/// </summary>
public class SagaClientOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Saga:Client";

    /// <summary>
    /// Saga 服务器地址列表
    /// </summary>
    public List<string> ServerUrls { get; set; } = ["http://localhost:5400"];

    /// <summary>
    /// 请求超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}
