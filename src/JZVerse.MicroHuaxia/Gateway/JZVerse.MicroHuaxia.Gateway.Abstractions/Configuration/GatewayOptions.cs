namespace JZVerse.MicroHuaxia.Gateway.Abstractions.Configuration;

/// <summary>
/// 网关配置选项
/// </summary>
public sealed class GatewayOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Gateway";

    /// <summary>
    /// 网关服务名称
    /// </summary>
    public string ServiceName { get; set; } = "api-gateway";

    /// <summary>
    /// 监听端口配置
    /// </summary>
    public ListenPortsOptions ListenPorts { get; set; } = new();

    /// <summary>
    /// 服务发现配置
    /// </summary>
    public ServiceDiscoveryOptions ServiceDiscovery { get; set; } = new();

    /// <summary>
    /// 配置中心配置
    /// </summary>
    public ConfigCenterOptions ConfigCenter { get; set; } = new();

    /// <summary>
    /// 动态路由配置
    /// </summary>
    public DynamicRoutingOptions DynamicRouting { get; set; } = new();

    /// <summary>
    /// 默认超时时间
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 最大并发请求数
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = 1000;

    /// <summary>
    /// 是否启用指标收集
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// 是否启用审计日志
    /// </summary>
    public bool EnableAuditLog { get; set; } = true;

    /// <summary>
    /// 是否启用请求/响应日志
    /// </summary>
    public bool EnableRequestResponseLog { get; set; }

    /// <summary>
    /// 密钥加密主密钥（用于加密存储敏感配置）
    /// </summary>
    public string? EncryptionMasterKey { get; set; }
}

/// <summary>
/// 监听端口配置
/// </summary>
public sealed class ListenPortsOptions
{
    /// <summary>
    /// HTTP 端口
    /// </summary>
    public int Http { get; set; } = 5000;

    /// <summary>
    /// HTTPS 端口
    /// </summary>
    public int Https { get; set; } = 5001;

    /// <summary>
    /// 管理 API 端口
    /// </summary>
    public int Management { get; set; } = 5002;

    /// <summary>
    /// gRPC 端口
    /// </summary>
    public int Grpc { get; set; } = 5003;
}

/// <summary>
/// 服务发现配置
/// </summary>
public sealed class ServiceDiscoveryOptions
{
    /// <summary>
    /// 是否启用服务发现
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 是否使用本地服务发现（聚合部署时）
    /// </summary>
    public bool UseLocal { get; set; } = true;

    /// <summary>
    /// 服务发现服务器地址（非聚合部署时）
    /// </summary>
    public List<string> ServerUrls { get; set; } = [];
}

/// <summary>
/// 配置中心配置
/// </summary>
public sealed class ConfigCenterOptions
{
    /// <summary>
    /// 是否启用配置中心
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 是否使用本地配置中心（聚合部署时）
    /// </summary>
    public bool UseLocal { get; set; } = true;

    /// <summary>
    /// 配置中心服务器地址（非聚合部署时）
    /// </summary>
    public List<string> ServerUrls { get; set; } = [];

    /// <summary>
    /// 应用 ID
    /// </summary>
    public string ApplicationId { get; set; } = "gateway";

    /// <summary>
    /// 环境 ID
    /// </summary>
    public string EnvironmentId { get; set; } = "production";

    /// <summary>
    /// 路由配置命名空间
    /// </summary>
    public string RoutesNamespace { get; set; } = "gateway.routes";

    /// <summary>
    /// 认证策略配置命名空间
    /// </summary>
    public string AuthenticationNamespace { get; set; } = "gateway.authentication";
}

/// <summary>
/// 动态路由配置
/// </summary>
public sealed class DynamicRoutingOptions
{
    /// <summary>
    /// 是否启用动态路由
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 配置刷新间隔（秒）
    /// </summary>
    public int RefreshIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// 配置来源
    /// </summary>
    public RouteConfigSource Source { get; set; } = RouteConfigSource.ConfigCenter;

    /// <summary>
    /// 文件路径（Source 为 File 时使用）
    /// </summary>
    public string? FilePath { get; set; }
}

/// <summary>
/// 路由配置来源
/// </summary>
public enum RouteConfigSource
{
    /// <summary>
    /// 从配置中心加载
    /// </summary>
    ConfigCenter,

    /// <summary>
    /// 从文件加载
    /// </summary>
    File,

    /// <summary>
    /// 从 appsettings.json 加载
    /// </summary>
    Configuration
}
