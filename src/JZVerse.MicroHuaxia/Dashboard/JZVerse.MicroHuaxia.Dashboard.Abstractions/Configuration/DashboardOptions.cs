namespace JZVerse.MicroHuaxia.Dashboard.Abstractions.Configuration;

/// <summary>
/// Dashboard 配置选项
/// </summary>
public class DashboardOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Dashboard";

    /// <summary>
    /// Dashboard 标题
    /// </summary>
    public string Title { get; set; } = "MicroHuaxia 管理控制台";

    /// <summary>
    /// 是否启用实时监控
    /// </summary>
    public bool EnableRealTimeMonitoring { get; set; } = true;

    /// <summary>
    /// 数据刷新间隔（秒）
    /// </summary>
    public int RefreshIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// 服务端点配置
    /// </summary>
    public ServiceEndpointsOptions ServiceEndpoints { get; set; } = new();

    /// <summary>
    /// 主题设置
    /// </summary>
    public ThemeOptions Theme { get; set; } = new();
}

/// <summary>
/// 服务端点配置
/// </summary>
public class ServiceEndpointsOptions
{
    /// <summary>
    /// 服务发现服务地址
    /// </summary>
    public string ServiceDiscovery { get; set; } = "http://localhost:5000";

    /// <summary>
    /// 配置中心服务地址
    /// </summary>
    public string ConfigCenter { get; set; } = "http://localhost:5000";

    /// <summary>
    /// 网关服务地址
    /// </summary>
    public string Gateway { get; set; } = "http://localhost:5000";

    /// <summary>
    /// 消息队列服务地址
    /// </summary>
    public string MessageQueue { get; set; } = "http://localhost:5000";

    /// <summary>
    /// Saga 服务地址
    /// </summary>
    public string Saga { get; set; } = "http://localhost:5000";

    /// <summary>
    /// ProcessManager 服务地址
    /// </summary>
    public string ProcessManager { get; set; } = "http://localhost:5200";
}

/// <summary>
/// 主题选项
/// </summary>
public class ThemeOptions
{
    /// <summary>
    /// 主题色
    /// </summary>
    public string PrimaryColor { get; set; } = "#1890ff";

    /// <summary>
    /// 主题模式：light | dark | auto
    /// </summary>
    public string Mode { get; set; } = "light";
}
