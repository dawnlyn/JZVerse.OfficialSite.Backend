namespace JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Models;

/// <summary>
/// .NET 项目资源
/// </summary>
public sealed class ProjectResource : Resource
{
    /// <summary>
    /// 项目路径 (csproj 文件路径)
    /// </summary>
    public string ProjectPath { get; }

    /// <summary>
    /// 启动配置
    /// </summary>
    public string? LaunchProfile { get; set; }

    /// <summary>
    /// HTTP 端点
    /// </summary>
    public List<EndpointConfig> Endpoints { get; } = [];

    /// <summary>
    /// 命令行参数
    /// </summary>
    public List<string> Args { get; } = [];

    /// <summary>
    /// 工作目录
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// 副本数
    /// </summary>
    public int Replicas { get; set; } = 1;

    /// <inheritdoc />
    public override ResourceType Type => ResourceType.Project;

    public ProjectResource(string name, string projectPath)
        : base(name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ProjectPath = projectPath;
    }
}

/// <summary>
/// 端点配置
/// </summary>
public sealed class EndpointConfig
{
    /// <summary>
    /// 端点名称
    /// </summary>
    public string Name { get; set; } = "http";

    /// <summary>
    /// 协议 (http/https/tcp)
    /// </summary>
    public string Scheme { get; set; } = "http";

    /// <summary>
    /// 主机
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// 端口 (null 表示自动分配)
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// 容器端口 (用于容器资源)
    /// </summary>
    public int? ContainerPort { get; set; }

    /// <summary>
    /// 是否外部可访问
    /// </summary>
    public bool IsExternal { get; set; }

    /// <summary>
    /// 生成 URL
    /// </summary>
    public string GetUrl()
    {
        var port = Port ?? ContainerPort ?? 80;
        return $"{Scheme}://{Host}:{port}";
    }
}
