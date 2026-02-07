namespace JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Models;

/// <summary>
/// 可执行程序资源
/// </summary>
public sealed class ExecutableResource : Resource
{
    /// <summary>
    /// 可执行文件路径
    /// </summary>
    public string ExecutablePath { get; }

    /// <summary>
    /// 命令行参数
    /// </summary>
    public List<string> Args { get; } = [];

    /// <summary>
    /// 工作目录
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// 端点配置
    /// </summary>
    public List<EndpointConfig> Endpoints { get; } = [];

    /// <inheritdoc />
    public override ResourceType Type => ResourceType.Executable;

    public ExecutableResource(string name, string executablePath)
        : base(name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ExecutablePath = executablePath;
    }
}

/// <summary>
/// 外部服务资源 (引用现有服务)
/// </summary>
public sealed class ExternalServiceResource : Resource
{
    /// <summary>
    /// 服务 URL
    /// </summary>
    public string Url { get; }

    /// <inheritdoc />
    public override ResourceType Type => ResourceType.ExternalService;

    public ExternalServiceResource(string name, string url)
        : base(name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        Url = url;
    }
}
