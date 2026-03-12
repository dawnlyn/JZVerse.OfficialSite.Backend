namespace JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Models;

/// <summary>
/// 前端项目资源
/// </summary>
public sealed class FrontendResource : Resource
{
    /// <summary>
    /// 前端项目根目录路径
    /// </summary>
    public string ProjectPath { get; }

    /// <summary>
    /// 部署模式
    /// </summary>
    public DeploymentMode DeploymentMode { get; internal set; } = DeploymentMode.LocalDevelopment;

    /// <summary>
    /// 包管理器类型 (本地开发模式使用)
    /// </summary>
    public PackageManager PackageManager { get; set; } = PackageManager.Auto;

    /// <summary>
    /// 启动命令 (本地开发模式，默认 "dev")
    /// </summary>
    public string StartCommand { get; set; } = "dev";

    /// <summary>
    /// 构建命令 (默认 "build")
    /// </summary>
    public string BuildCommand { get; set; } = "build";

    /// <summary>
    /// 工作目录
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// HTTP 端点配置
    /// </summary>
    public List<EndpointConfig> Endpoints { get; } = [];

    /// <summary>
    /// Docker 部署配置 (容器模式使用)
    /// </summary>
    public DockerDeploymentConfig DockerConfig { get; } = new();

    /// <summary>
    /// 检测到的前端框架类型 (用于环境变量前缀)
    /// </summary>
    public FrontendFramework DetectedFramework { get; internal set; } = FrontendFramework.Unknown;

    /// <inheritdoc />
    public override ResourceType Type => ResourceType.Frontend;

    /// <summary>
    /// 创建前端资源 (本地开发模式)
    /// </summary>
    /// <param name="name">资源名称</param>
    /// <param name="projectPath">前端项目根目录路径</param>
    public FrontendResource(string name, string projectPath)
        : base(name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ProjectPath = projectPath;
    }

    /// <summary>
    /// 创建前端资源 (容器模式 - 现有镜像)
    /// </summary>
    /// <param name="name">资源名称</param>
    /// <param name="image">容器镜像名称</param>
    /// <param name="deploymentMode">部署模式</param>
    internal FrontendResource(string name, string image, DeploymentMode deploymentMode)
        : base(name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(image);
        ProjectPath = string.Empty;
        DeploymentMode = deploymentMode;

        if (deploymentMode == DeploymentMode.ContainerExisting)
        {
            DockerConfig.ContainerImage = image;
        }
        else if (deploymentMode == DeploymentMode.ContainerGit)
        {
            DockerConfig.GitRepositoryUrl = image;
        }
    }
}

/// <summary>
/// Docker 部署配置
/// </summary>
public sealed class DockerDeploymentConfig
{
    /// <summary>
    /// 容器镜像名称 (ContainerExisting 模式)
    /// </summary>
    public string? ContainerImage { get; set; }

    /// <summary>
    /// 镜像标签
    /// </summary>
    public string ImageTag { get; set; } = "latest";

    /// <summary>
    /// Dockerfile 路径 (ContainerDockerfile 模式)
    /// </summary>
    public string? DockerfilePath { get; set; }

    /// <summary>
    /// Docker 构建上下文路径
    /// </summary>
    public string? ContextPath { get; set; }

    /// <summary>
    /// Git 仓库地址 (ContainerGit 模式)
    /// </summary>
    public string? GitRepositoryUrl { get; set; }

    /// <summary>
    /// Git 分支名称
    /// </summary>
    public string GitBranch { get; set; } = "main";

    /// <summary>
    /// Docker 构建参数
    /// </summary>
    public Dictionary<string, string> BuildArgs { get; } = new();

    /// <summary>
    /// 卷挂载配置
    /// </summary>
    public List<VolumeMount> Volumes { get; } = [];

    /// <summary>
    /// 容器名称
    /// </summary>
    public string? ContainerName { get; set; }

    /// <summary>
    /// 是否总是拉取镜像
    /// </summary>
    public bool AlwaysPull { get; set; }

    /// <summary>
    /// 获取完整镜像名称
    /// </summary>
    public string GetFullImageName()
    {
        if (string.IsNullOrEmpty(ContainerImage))
            throw new InvalidOperationException("Container image is not set");
        return $"{ContainerImage}:{ImageTag}";
    }
}
