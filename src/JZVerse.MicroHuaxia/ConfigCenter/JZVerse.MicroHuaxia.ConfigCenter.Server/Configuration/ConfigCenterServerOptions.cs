namespace JZVerse.MicroHuaxia.ConfigCenter.Server.Configuration;

/// <summary>
/// 配置中心服务端配置选项
/// </summary>
public class ConfigCenterServerOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "ConfigCenter:Server";

    /// <summary>
    /// 服务名称
    /// </summary>
    public string ServiceName { get; set; } = "config-center-server";

    /// <summary>
    /// 是否启用缓存
    /// </summary>
    public bool EnableCache { get; set; } = true;

    /// <summary>
    /// 缓存 TTL（秒）
    /// </summary>
    public int CacheTtlSeconds { get; set; } = 60;

    /// <summary>
    /// 是否启用持久化
    /// </summary>
    public bool EnablePersistence { get; set; } = true;

    /// <summary>
    /// 持久化路径
    /// </summary>
    public string PersistencePath { get; set; } = "./data/config";

    /// <summary>
    /// 快照间隔（分钟）
    /// </summary>
    public int SnapshotIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// 长轮询超时时间（秒）
    /// </summary>
    public int LongPollingTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 是否启用 WebSocket
    /// </summary>
    public bool EnableWebSocket { get; set; } = true;
}
