using JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Helpers;
using JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Models;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Builder;

/// <summary>
/// 前端资源构建器
/// </summary>
public sealed class FrontendResourceBuilder
{
    internal FrontendResourceBuilder(DistributedApplicationBuilder builder, FrontendResource resource)
    {
        Builder = builder;
        Resource = resource;
    }

    /// <summary>
    /// 获取资源实例
    /// </summary>
    public FrontendResource Resource { get; }

    /// <summary>
    /// 返回主构建器
    /// </summary>
    public DistributedApplicationBuilder Builder { get; }

    #region 通用方法

    /// <summary>
    /// 添加 HTTP 端点
    /// </summary>
    /// <param name="port">端口号 (null 表示自动分配)</param>
    /// <param name="name">端点名称</param>
    public FrontendResourceBuilder WithHttpEndpoint(int? port = null, string name = "http")
    {
        Resource.Endpoints.Add(
            new EndpointConfig
            {
                Name = name,
                Scheme = "http",
                Port = port,
            }
        );
        return this;
    }

    /// <summary>
    /// 添加 HTTP 端点 (容器模式，指定容器端口和主机端口)
    /// </summary>
    /// <param name="containerPort">容器端口</param>
    /// <param name="hostPort">主机端口 (null 表示自动分配)</param>
    /// <param name="name">端点名称</param>
    public FrontendResourceBuilder WithHttpEndpoint(int containerPort, int? hostPort, string name = "http")
    {
        Resource.Endpoints.Add(
            new EndpointConfig
            {
                Name = name,
                Scheme = "http",
                Port = hostPort,
                ContainerPort = containerPort,
            }
        );
        return this;
    }

    /// <summary>
    /// 设置环境变量
    /// </summary>
    public FrontendResourceBuilder WithEnvironment(string name, string value)
    {
        Resource.Environment[name] = value;
        return this;
    }

    /// <summary>
    /// 从另一个资源引用，自动注入服务 URL 环境变量
    /// </summary>
    /// <remarks>
    /// 会根据检测到的前端框架自动添加正确的环境变量前缀：
    /// - Vite: VITE_xxx
    /// - Next.js: NEXT_PUBLIC_xxx
    /// - Nuxt.js: NUXT_PUBLIC_xxx
    /// - CRA: REACT_APP_xxx
    /// - Vue CLI: VUE_APP_xxx
    /// </remarks>
    public FrontendResourceBuilder WithReference(Resource resource)
    {
        Resource.Dependencies.Add(resource);

        // 获取服务端点 URL
        var endpoints = GetResourceEndpoints(resource);
        var framework = Resource.DetectedFramework;

        foreach (var (endpointName, url) in endpoints)
        {
            var envVarName = FrontendFrameworkDetector.GetServiceUrlEnvironmentVariableName(
                framework,
                resource.Name,
                endpointName
            );

            // 仅当环境变量未被手动设置时才注入
            if (!Resource.Environment.ContainsKey(envVarName))
            {
                Resource.Environment[envVarName] = url;
            }
        }

        return this;
    }

    #endregion

    #region 本地开发模式专用

    /// <summary>
    /// 设置包管理器
    /// </summary>
    /// <param name="manager">包管理器类型</param>
    public FrontendResourceBuilder WithPackageManager(PackageManager manager)
    {
        Resource.PackageManager = manager;
        return this;
    }

    /// <summary>
    /// 设置启动命令 (默认 "dev")
    /// </summary>
    /// <param name="command">命令名称，如 "dev", "start", "serve"</param>
    public FrontendResourceBuilder WithCommand(string command)
    {
        Resource.StartCommand = command;
        return this;
    }

    /// <summary>
    /// 设置构建命令 (默认 "build")
    /// </summary>
    public FrontendResourceBuilder WithBuildCommand(string command)
    {
        Resource.BuildCommand = command;
        return this;
    }

    /// <summary>
    /// 设置工作目录
    /// </summary>
    public FrontendResourceBuilder WithWorkingDirectory(string workingDirectory)
    {
        Resource.WorkingDirectory = workingDirectory;
        return this;
    }

    #endregion

    #region 容器模式专用

    /// <summary>
    /// 指定 Dockerfile 路径，切换到 ContainerDockerfile 模式
    /// </summary>
    /// <param name="dockerfilePath">Dockerfile 文件路径</param>
    public FrontendResourceBuilder WithDockerfile(string dockerfilePath)
    {
        Resource.DeploymentMode = DeploymentMode.ContainerDockerfile;
        Resource.DockerConfig.DockerfilePath = dockerfilePath;

        // 默认上下文路径为 Dockerfile 所在目录
        if (string.IsNullOrEmpty(Resource.DockerConfig.ContextPath))
        {
            Resource.DockerConfig.ContextPath = Path.GetDirectoryName(dockerfilePath);
        }

        return this;
    }

    /// <summary>
    /// 设置 Docker 构建上下文路径
    /// </summary>
    public FrontendResourceBuilder WithContextPath(string contextPath)
    {
        Resource.DockerConfig.ContextPath = contextPath;
        return this;
    }

    /// <summary>
    /// 设置镜像标签
    /// </summary>
    public FrontendResourceBuilder WithTag(string tag)
    {
        Resource.DockerConfig.ImageTag = tag;
        return this;
    }

    /// <summary>
    /// 设置 Git 分支 (ContainerGit 模式)
    /// </summary>
    public FrontendResourceBuilder WithBranch(string branch)
    {
        Resource.DockerConfig.GitBranch = branch;
        return this;
    }

    /// <summary>
    /// 添加 Docker 构建参数
    /// </summary>
    public FrontendResourceBuilder WithBuildArg(string key, string value)
    {
        Resource.DockerConfig.BuildArgs[key] = value;
        return this;
    }

    /// <summary>
    /// 添加绑定挂载
    /// </summary>
    public FrontendResourceBuilder WithBindMount(string source, string target, bool readOnly = false)
    {
        Resource.DockerConfig.Volumes.Add(
            new VolumeMount
            {
                Type = VolumeMountType.Bind,
                Source = source,
                Target = target,
                ReadOnly = readOnly,
            }
        );
        return this;
    }

    /// <summary>
    /// 添加命名卷
    /// </summary>
    public FrontendResourceBuilder WithVolume(string volumeName, string target, bool readOnly = false)
    {
        Resource.DockerConfig.Volumes.Add(
            new VolumeMount
            {
                Type = VolumeMountType.Volume,
                Source = volumeName,
                Target = target,
                ReadOnly = readOnly,
            }
        );
        return this;
    }

    /// <summary>
    /// 设置容器名称
    /// </summary>
    public FrontendResourceBuilder WithContainerName(string name)
    {
        Resource.DockerConfig.ContainerName = name;
        return this;
    }

    /// <summary>
    /// 设置是否总是拉取镜像
    /// </summary>
    public FrontendResourceBuilder WithAlwaysPull(bool alwaysPull = true)
    {
        Resource.DockerConfig.AlwaysPull = alwaysPull;
        return this;
    }

    #endregion

    #region 辅助方法

    private static IEnumerable<(string Name, string Url)> GetResourceEndpoints(Resource resource)
    {
        switch (resource)
        {
            case ProjectResource project:
                foreach (var endpoint in project.Endpoints)
                {
                    yield return (endpoint.Name, endpoint.GetUrl());
                }
                break;
            case ContainerResource container:
                foreach (var endpoint in container.Endpoints)
                {
                    yield return (endpoint.Name, endpoint.GetUrl());
                }
                break;
            case ExecutableResource executable:
                foreach (var endpoint in executable.Endpoints)
                {
                    yield return (endpoint.Name, endpoint.GetUrl());
                }
                break;
            case FrontendResource frontend:
                foreach (var endpoint in frontend.Endpoints)
                {
                    yield return (endpoint.Name, endpoint.GetUrl());
                }
                break;
            case ExternalServiceResource external:
                yield return ("http", external.Url);
                break;
        }
    }

    #endregion

    /// <summary>
    /// 隐式转换为资源
    /// </summary>
    public static implicit operator FrontendResource(FrontendResourceBuilder builder) => builder.Resource;
}
