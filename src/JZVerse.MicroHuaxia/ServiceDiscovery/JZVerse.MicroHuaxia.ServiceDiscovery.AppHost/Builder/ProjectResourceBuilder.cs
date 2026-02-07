using JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Models;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Builder;

/// <summary>
/// 项目资源构建器
/// </summary>
public sealed class ProjectResourceBuilder
{
    internal ProjectResourceBuilder(DistributedApplicationBuilder builder, ProjectResource resource)
    {
        Builder = builder;
        Resource = resource;
    }

    /// <summary>
    /// 添加 HTTP 端点
    /// </summary>
    public ProjectResourceBuilder WithHttpEndpoint(int? port = null, string name = "http")
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
    /// 添加 HTTPS 端点
    /// </summary>
    public ProjectResourceBuilder WithHttpsEndpoint(int? port = null, string name = "https")
    {
        Resource.Endpoints.Add(
            new()
            {
                Name = name,
                Scheme = "https",
                Port = port,
            }
        );
        return this;
    }

    /// <summary>
    /// 设置环境变量
    /// </summary>
    public ProjectResourceBuilder WithEnvironment(string name, string value)
    {
        Resource.Environment[name] = value;
        return this;
    }

    /// <summary>
    /// 从另一个资源引用环境变量
    /// </summary>
    public ProjectResourceBuilder WithReference(Resource resource)
    {
        Resource.Dependencies.Add(resource);

        // 自动注入服务发现相关的环境变量
        var envPrefix = $"services__{resource.Name}__";

        switch (resource)
        {
            case ProjectResource project:
                foreach (var endpoint in project.Endpoints)
                {
                    Resource.Environment[$"{envPrefix}{endpoint.Name}__0"] = endpoint.GetUrl();
                }
                break;
            case ContainerResource container:
                foreach (var endpoint in container.Endpoints)
                {
                    Resource.Environment[$"{envPrefix}{endpoint.Name}__0"] = endpoint.GetUrl();
                }
                break;
            case ExternalServiceResource external:
                Resource.Environment[$"{envPrefix}http__0"] = external.Url;
                break;
        }

        return this;
    }

    /// <summary>
    /// 添加命令行参数
    /// </summary>
    public ProjectResourceBuilder WithArgs(params string[] args)
    {
        Resource.Args.AddRange(args);
        return this;
    }

    /// <summary>
    /// 设置启动配置
    /// </summary>
    public ProjectResourceBuilder WithLaunchProfile(string profileName)
    {
        Resource.LaunchProfile = profileName;
        return this;
    }

    /// <summary>
    /// 设置副本数
    /// </summary>
    public ProjectResourceBuilder WithReplicas(int count)
    {
        Resource.Replicas = count;
        return this;
    }

    /// <summary>
    /// 获取资源实例
    /// </summary>
    public ProjectResource Resource { get; }

    /// <summary>
    /// 返回主构建器
    /// </summary>
    public DistributedApplicationBuilder Builder { get; }

    /// <summary>
    /// 隐式转换为资源
    /// </summary>
    public static implicit operator ProjectResource(ProjectResourceBuilder builder) => builder.Resource;
}
