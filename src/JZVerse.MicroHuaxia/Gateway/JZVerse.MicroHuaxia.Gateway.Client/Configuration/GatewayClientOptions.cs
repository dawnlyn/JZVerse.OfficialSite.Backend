namespace JZVerse.MicroHuaxia.Gateway.Client.Configuration;

/// <summary>
/// 网关客户端配置选项
/// </summary>
public class GatewayClientOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Gateway:Client";

    /// <summary>
    /// 网关管理端服务器地址列表
    /// </summary>
    public List<string> ServerUrls { get; set; } = ["http://localhost:5202"];

    /// <summary>
    /// 请求超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}
