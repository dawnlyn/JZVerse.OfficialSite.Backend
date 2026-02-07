using YamlDotNet.Serialization;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Configuration;

/// <summary>
/// 应用配置 (YAML/JSON 格式)
/// </summary>
public sealed class AppHostConfiguration
{
    /// <summary>
    /// 应用名称
    /// </summary>
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = "DistributedApp";

    /// <summary>
    /// 项目资源
    /// </summary>
    [YamlMember(Alias = "projects")]
    public List<ProjectConfig> Projects { get; set; } = [];

    /// <summary>
    /// 容器资源
    /// </summary>
    [YamlMember(Alias = "containers")]
    public List<ContainerConfig> Containers { get; set; } = [];

    /// <summary>
    /// 可执行程序资源
    /// </summary>
    [YamlMember(Alias = "executables")]
    public List<ExecutableConfig> Executables { get; set; } = [];

    /// <summary>
    /// 外部服务
    /// </summary>
    [YamlMember(Alias = "external_services")]
    public List<ExternalServiceConfig> ExternalServices { get; set; } = [];
}

/// <summary>
/// 项目配置
/// </summary>
public sealed class ProjectConfig
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "path")]
    public string Path { get; set; } = string.Empty;

    [YamlMember(Alias = "launch_profile")]
    public string? LaunchProfile { get; set; }

    [YamlMember(Alias = "endpoints")]
    public List<EndpointConfigYaml> Endpoints { get; set; } = [];

    [YamlMember(Alias = "environment")]
    public Dictionary<string, string> Environment { get; set; } = new();

    [YamlMember(Alias = "args")]
    public List<string> Args { get; set; } = [];

    [YamlMember(Alias = "replicas")]
    public int Replicas { get; set; } = 1;

    [YamlMember(Alias = "depends_on")]
    public List<string> DependsOn { get; set; } = [];
}

/// <summary>
/// 容器配置
/// </summary>
public sealed class ContainerConfig
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "image")]
    public string Image { get; set; } = string.Empty;

    [YamlMember(Alias = "tag")]
    public string Tag { get; set; } = "latest";

    [YamlMember(Alias = "endpoints")]
    public List<EndpointConfigYaml> Endpoints { get; set; } = [];

    [YamlMember(Alias = "environment")]
    public Dictionary<string, string> Environment { get; set; } = new();

    [YamlMember(Alias = "volumes")]
    public List<VolumeConfigYaml> Volumes { get; set; } = [];

    [YamlMember(Alias = "args")]
    public List<string> Args { get; set; } = [];

    [YamlMember(Alias = "depends_on")]
    public List<string> DependsOn { get; set; } = [];
}

/// <summary>
/// 可执行程序配置
/// </summary>
public sealed class ExecutableConfig
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "path")]
    public string Path { get; set; } = string.Empty;

    [YamlMember(Alias = "working_directory")]
    public string? WorkingDirectory { get; set; }

    [YamlMember(Alias = "args")]
    public List<string> Args { get; set; } = [];

    [YamlMember(Alias = "environment")]
    public Dictionary<string, string> Environment { get; set; } = new();

    [YamlMember(Alias = "depends_on")]
    public List<string> DependsOn { get; set; } = [];
}

/// <summary>
/// 外部服务配置
/// </summary>
public sealed class ExternalServiceConfig
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "url")]
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// 端点配置 (YAML)
/// </summary>
public sealed class EndpointConfigYaml
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = "http";

    [YamlMember(Alias = "scheme")]
    public string Scheme { get; set; } = "http";

    [YamlMember(Alias = "port")]
    public int? Port { get; set; }

    [YamlMember(Alias = "container_port")]
    public int? ContainerPort { get; set; }
}

/// <summary>
/// 卷配置 (YAML)
/// </summary>
public sealed class VolumeConfigYaml
{
    [YamlMember(Alias = "source")]
    public string Source { get; set; } = string.Empty;

    [YamlMember(Alias = "target")]
    public string Target { get; set; } = string.Empty;

    [YamlMember(Alias = "read_only")]
    public bool ReadOnly { get; set; }
}
