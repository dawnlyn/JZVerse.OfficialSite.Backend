using JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Models;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Builder;

/// <summary>
/// 分布式应用构建器
/// </summary>
public sealed class DistributedApplicationBuilder
{
    private readonly List<Resource> _resources = [];
    private readonly Dictionary<string, object> _parameters = new();

    /// <summary>
    /// 应用名称
    /// </summary>
    public string ApplicationName { get; set; } = "DistributedApp";

    /// <summary>
    /// 基础路径
    /// </summary>
    public string? BasePath { get; set; }

    /// <summary>
    /// 所有资源
    /// </summary>
    public IReadOnlyList<Resource> Resources => _resources.AsReadOnly();

    /// <summary>
    /// 添加 .NET 项目
    /// </summary>
    public ProjectResourceBuilder AddProject(string name, string projectPath)
    {
        var resource = new ProjectResource(name, ResolveProjectPath(projectPath));
        _resources.Add(resource);
        return new(this, resource);
    }

    /// <summary>
    /// 添加 .NET 项目 (泛型方式)
    /// </summary>
    public ProjectResourceBuilder AddProject<TProject>(string name)
        where TProject : class
    {
        var assembly = typeof(TProject).Assembly;
        var projectPath = FindProjectPath(assembly.Location);
        return AddProject(name, projectPath);
    }

    /// <summary>
    /// 添加容器
    /// </summary>
    public ContainerResourceBuilder AddContainer(string name, string image)
    {
        var resource = new ContainerResource(name, image);
        _resources.Add(resource);
        return new(this, resource);
    }

    /// <summary>
    /// 添加可执行程序
    /// </summary>
    public ExecutableResourceBuilder AddExecutable(string name, string executablePath)
    {
        var resource = new ExecutableResource(name, executablePath);
        _resources.Add(resource);
        return new(this, resource);
    }

    /// <summary>
    /// 添加外部服务引用
    /// </summary>
    public DistributedApplicationBuilder AddExternalService(string name, string url)
    {
        var resource = new ExternalServiceResource(name, url);
        _resources.Add(resource);
        return this;
    }

    /// <summary>
    /// 添加参数
    /// </summary>
    public DistributedApplicationBuilder WithParameter(string name, object value)
    {
        _parameters[name] = value;
        return this;
    }

    /// <summary>
    /// 获取参数
    /// </summary>
    public T? GetParameter<T>(string name) => _parameters.TryGetValue(name, out var value) ? (T)value : default(T?);

    /// <summary>
    /// 构建分布式应用
    /// </summary>
    public DistributedApplication Build()
    {
        ValidateResources();
        return new(ApplicationName, _resources, _parameters);
    }

    private string ResolveProjectPath(string projectPath)
    {
        if (Path.IsPathRooted(projectPath))
            return projectPath;

        return !string.IsNullOrEmpty(BasePath) ? Path.Combine(BasePath, projectPath) : Path.GetFullPath(projectPath);
    }

    private static string FindProjectPath(string assemblyLocation)
    {
        var directory = Path.GetDirectoryName(assemblyLocation);
        while (directory != null)
        {
            var csprojFiles = Directory.GetFiles(directory, "*.csproj");
            if (csprojFiles.Length > 0)
                return csprojFiles[0];
            directory = Path.GetDirectoryName(directory);
        }
        throw new InvalidOperationException($"Could not find project file for assembly: {assemblyLocation}");
    }

    private void ValidateResources()
    {
        var names = new HashSet<string>();
        foreach (var resource in _resources)
        {
            if (!names.Add(resource.Name))
                throw new InvalidOperationException($"Duplicate resource name: {resource.Name}");
        }

        // 验证依赖是否存在
        foreach (var resource in _resources)
        {
            foreach (var dep in resource.Dependencies)
            {
                if (!_resources.Contains(dep))
                    throw new InvalidOperationException(
                        $"Resource '{resource.Name}' depends on unknown resource '{dep.Name}'"
                    );
            }
        }
    }
}

/// <summary>
/// 分布式应用
/// </summary>
public sealed class DistributedApplication
{
    /// <summary>
    /// 应用名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 所有资源
    /// </summary>
    public IReadOnlyList<Resource> Resources { get; }

    /// <summary>
    /// 参数
    /// </summary>
    public IReadOnlyDictionary<string, object> Parameters { get; }

    internal DistributedApplication(string name, List<Resource> resources, Dictionary<string, object> parameters)
    {
        Name = name;
        Resources = resources.AsReadOnly();
        Parameters = parameters;
    }

    /// <summary>
    /// 获取指定类型的资源
    /// </summary>
    public IEnumerable<T> GetResources<T>()
        where T : Resource
    {
        return Resources.OfType<T>();
    }

    /// <summary>
    /// 获取指定名称的资源
    /// </summary>
    public Resource? GetResource(string name)
    {
        return Resources.FirstOrDefault(r => r.Name == name);
    }

    /// <summary>
    /// 获取指定名称的资源
    /// </summary>
    public T? GetResource<T>(string name)
        where T : Resource
    {
        return Resources.OfType<T>().FirstOrDefault(r => r.Name == name);
    }
}
