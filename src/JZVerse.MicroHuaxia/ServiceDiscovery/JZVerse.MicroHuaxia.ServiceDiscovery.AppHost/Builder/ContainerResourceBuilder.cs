using JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Models;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Builder;

/// <summary>
/// 容器资源构建器
/// </summary>
public sealed class ContainerResourceBuilder
{
    internal ContainerResourceBuilder(DistributedApplicationBuilder builder, ContainerResource resource)
    {
        Builder = builder;
        Resource = resource;
    }

    /// <summary>
    /// 设置镜像标签
    /// </summary>
    public ContainerResourceBuilder WithTag(string tag)
    {
        Resource.Tag = tag;
        return this;
    }

    /// <summary>
    /// 添加端口映射
    /// </summary>
    public ContainerResourceBuilder WithEndpoint(
        int containerPort,
        int? hostPort = null,
        string name = "default",
        string scheme = "tcp"
    )
    {
        Resource.Endpoints.Add(
            new()
            {
                Name = name,
                Scheme = scheme,
                Port = hostPort,
                ContainerPort = containerPort,
            }
        );
        return this;
    }

    /// <summary>
    /// 添加 HTTP 端点
    /// </summary>
    public ContainerResourceBuilder WithHttpEndpoint(int containerPort, int? hostPort = null, string name = "http") =>
        WithEndpoint(containerPort, hostPort, name, "http");

    /// <summary>
    /// 设置环境变量
    /// </summary>
    public ContainerResourceBuilder WithEnvironment(string name, string value)
    {
        Resource.Environment[name] = value;
        return this;
    }

    /// <summary>
    /// 添加绑定挂载
    /// </summary>
    public ContainerResourceBuilder WithBindMount(string source, string target, bool readOnly = false)
    {
        Resource.Volumes.Add(
            new()
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
    public ContainerResourceBuilder WithVolume(string volumeName, string target, bool readOnly = false)
    {
        Resource.Volumes.Add(
            new()
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
    /// 添加命令行参数
    /// </summary>
    public ContainerResourceBuilder WithArgs(params string[] args)
    {
        Resource.Args.AddRange(args);
        return this;
    }

    /// <summary>
    /// 设置入口点
    /// </summary>
    public ContainerResourceBuilder WithEntrypoint(string entrypoint)
    {
        Resource.Entrypoint = entrypoint;
        return this;
    }

    /// <summary>
    /// 设置容器名称
    /// </summary>
    public ContainerResourceBuilder WithContainerName(string name)
    {
        Resource.ContainerName = name;
        return this;
    }

    /// <summary>
    /// 从另一个资源引用
    /// </summary>
    public ContainerResourceBuilder WithReference(Resource resource)
    {
        Resource.Dependencies.Add(resource);
        return this;
    }

    /// <summary>
    /// 获取资源实例
    /// </summary>
    public ContainerResource Resource { get; }

    /// <summary>
    /// 返回主构建器
    /// </summary>
    public DistributedApplicationBuilder Builder { get; }

    /// <summary>
    /// 隐式转换为资源
    /// </summary>
    public static implicit operator ContainerResource(ContainerResourceBuilder builder) => builder.Resource;
}

/// <summary>
/// 可执行程序资源构建器
/// </summary>
public sealed class ExecutableResourceBuilder
{
    internal ExecutableResourceBuilder(DistributedApplicationBuilder builder, ExecutableResource resource)
    {
        Builder = builder;
        Resource = resource;
    }

    /// <summary>
    /// 添加命令行参数
    /// </summary>
    public ExecutableResourceBuilder WithArgs(params string[] args)
    {
        Resource.Args.AddRange(args);
        return this;
    }

    /// <summary>
    /// 设置工作目录
    /// </summary>
    public ExecutableResourceBuilder WithWorkingDirectory(string workingDirectory)
    {
        Resource.WorkingDirectory = workingDirectory;
        return this;
    }

    /// <summary>
    /// 设置环境变量
    /// </summary>
    public ExecutableResourceBuilder WithEnvironment(string name, string value)
    {
        Resource.Environment[name] = value;
        return this;
    }

    /// <summary>
    /// 添加 HTTP 端点
    /// </summary>
    public ExecutableResourceBuilder WithHttpEndpoint(int? port = null, string name = "http")
    {
        Resource.Endpoints.Add(
            new()
            {
                Name = name,
                Scheme = "http",
                Port = port,
            }
        );
        return this;
    }

    /// <summary>
    /// 从另一个资源引用
    /// </summary>
    public ExecutableResourceBuilder WithReference(Resource resource)
    {
        Resource.Dependencies.Add(resource);
        return this;
    }

    /// <summary>
    /// 获取资源实例
    /// </summary>
    public ExecutableResource Resource { get; }

    /// <summary>
    /// 返回主构建器
    /// </summary>
    public DistributedApplicationBuilder Builder { get; }

    /// <summary>
    /// 隐式转换为资源
    /// </summary>
    public static implicit operator ExecutableResource(ExecutableResourceBuilder builder) => builder.Resource;
}
