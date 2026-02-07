namespace JZVerse.MicroHuaxia.ConfigCenter.Client.Configuration;

/// <summary>
/// 配置中心客户端配置选项
/// </summary>
public class ConfigCenterClientOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "ConfigCenter:Client";

    /// <summary>
    /// 服务端地址列表
    /// </summary>
    public List<string> ServerUrls { get; set; } = ["http://localhost:5100"];

    /// <summary>
    /// 应用 ID
    /// </summary>
    public string ApplicationId { get; set; } = string.Empty;

    /// <summary>
    /// 环境 ID
    /// </summary>
    public string EnvironmentId { get; set; } = "dev";

    /// <summary>
    /// 命名空间列表
    /// </summary>
    public List<string> Namespaces { get; set; } = [];

    /// <summary>
    /// 通知模式
    /// </summary>
    public NotificationMode NotificationMode { get; set; } = NotificationMode.LongPolling;

    /// <summary>
    /// 轮询间隔（秒）
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// 是否启用本地缓存
    /// </summary>
    public bool EnableLocalCache { get; set; } = true;

    /// <summary>
    /// 本地缓存路径
    /// </summary>
    public string LocalCachePath { get; set; } = "./config-cache";

    /// <summary>
    /// 客户端 ID
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// 客户端标签
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// HTTP 超时时间（秒）
    /// </summary>
    public int HttpTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; set; } = 3;
}

/// <summary>
/// 通知模式
/// </summary>
public enum NotificationMode
{
    /// <summary>
    /// 长轮询
    /// </summary>
    LongPolling = 0,

    /// <summary>
    /// WebSocket
    /// </summary>
    WebSocket = 1,

    /// <summary>
    /// 主动拉取
    /// </summary>
    Polling = 2,
}
