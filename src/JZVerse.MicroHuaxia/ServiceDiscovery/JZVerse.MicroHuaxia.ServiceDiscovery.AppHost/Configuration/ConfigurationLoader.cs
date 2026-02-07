using System.Text.Json;
using JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Builder;
using JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Configuration;

/// <summary>
/// 配置加载器
/// </summary>
public static class ConfigurationLoader
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// 从文件加载配置
    /// </summary>
    public static AppHostConfiguration LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Configuration file not found: {filePath}");

        var content = File.ReadAllText(filePath);
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".yaml" or ".yml" => LoadFromYaml(content),
            ".json" => LoadFromJson(content),
            _ => throw new NotSupportedException($"Unsupported configuration file format: {extension}"),
        };
    }

    /// <summary>
    /// 从 YAML 字符串加载配置
    /// </summary>
    public static AppHostConfiguration LoadFromYaml(string yaml) =>
        YamlDeserializer.Deserialize<AppHostConfiguration>(yaml);

    /// <summary>
    /// 从 JSON 字符串加载配置
    /// </summary>
    public static AppHostConfiguration LoadFromJson(string json) =>
        JsonSerializer.Deserialize<AppHostConfiguration>(json, JsonOptions)
        ?? throw new InvalidOperationException("Failed to deserialize configuration");

    /// <summary>
    /// 将配置转换为 DistributedApplicationBuilder
    /// </summary>
    public static DistributedApplicationBuilder ToBuilder(this AppHostConfiguration config, string? basePath = null)
    {
        var builder = new DistributedApplicationBuilder { ApplicationName = config.Name, BasePath = basePath };

        var resourceMap = new Dictionary<string, Resource>();

        // 添加外部服务
        foreach (var external in config.ExternalServices)
        {
            builder.AddExternalService(external.Name, external.Url);
            resourceMap[external.Name] = builder.Resources.Last();
        }

        // 添加容器
        foreach (var container in config.Containers)
        {
            var containerBuilder = builder.AddContainer(container.Name, container.Image).WithTag(container.Tag);

            foreach (var endpoint in container.Endpoints)
            {
                containerBuilder.WithEndpoint(
                    endpoint.ContainerPort ?? endpoint.Port ?? 80,
                    endpoint.Port,
                    endpoint.Name,
                    endpoint.Scheme
                );
            }

            foreach (var (key, value) in container.Environment)
            {
                containerBuilder.WithEnvironment(key, value);
            }

            foreach (var volume in container.Volumes)
            {
                containerBuilder.WithBindMount(volume.Source, volume.Target, volume.ReadOnly);
            }

            if (container.Args.Count > 0)
            {
                containerBuilder.WithArgs(container.Args.ToArray());
            }

            resourceMap[container.Name] = containerBuilder.Resource;
        }

        // 添加可执行程序
        foreach (var executable in config.Executables)
        {
            var executableBuilder = builder.AddExecutable(executable.Name, executable.Path);

            if (!string.IsNullOrEmpty(executable.WorkingDirectory))
            {
                executableBuilder.WithWorkingDirectory(executable.WorkingDirectory);
            }

            foreach (var (key, value) in executable.Environment)
            {
                executableBuilder.WithEnvironment(key, value);
            }

            if (executable.Args.Count > 0)
            {
                executableBuilder.WithArgs(executable.Args.ToArray());
            }

            resourceMap[executable.Name] = executableBuilder.Resource;
        }

        // 添加项目
        foreach (var project in config.Projects)
        {
            var projectBuilder = builder.AddProject(project.Name, project.Path);

            if (!string.IsNullOrEmpty(project.LaunchProfile))
            {
                projectBuilder.WithLaunchProfile(project.LaunchProfile);
            }

            foreach (var endpoint in project.Endpoints)
            {
                if (endpoint.Scheme == "https")
                {
                    projectBuilder.WithHttpsEndpoint(endpoint.Port, endpoint.Name);
                }
                else
                {
                    projectBuilder.WithHttpEndpoint(endpoint.Port, endpoint.Name);
                }
            }

            foreach (var (key, value) in project.Environment)
            {
                projectBuilder.WithEnvironment(key, value);
            }

            if (project.Args.Count > 0)
            {
                projectBuilder.WithArgs(project.Args.ToArray());
            }

            projectBuilder.WithReplicas(project.Replicas);

            resourceMap[project.Name] = projectBuilder.Resource;
        }

        // 解析依赖关系
        ResolveDependencies(config, resourceMap);

        return builder;
    }

    private static void ResolveDependencies(AppHostConfiguration config, Dictionary<string, Resource> resourceMap)
    {
        foreach (var project in config.Projects)
        {
            if (resourceMap.TryGetValue(project.Name, out var resource))
            {
                foreach (var depName in project.DependsOn)
                {
                    if (resourceMap.TryGetValue(depName, out var dep))
                    {
                        resource.Dependencies.Add(dep);
                    }
                }
            }
        }

        foreach (var container in config.Containers)
        {
            if (resourceMap.TryGetValue(container.Name, out var resource))
            {
                foreach (var depName in container.DependsOn)
                {
                    if (resourceMap.TryGetValue(depName, out var dep))
                    {
                        resource.Dependencies.Add(dep);
                    }
                }
            }
        }

        foreach (var executable in config.Executables)
        {
            if (resourceMap.TryGetValue(executable.Name, out var resource))
            {
                foreach (var depName in executable.DependsOn)
                {
                    if (resourceMap.TryGetValue(depName, out var dep))
                    {
                        resource.Dependencies.Add(dep);
                    }
                }
            }
        }
    }
}
