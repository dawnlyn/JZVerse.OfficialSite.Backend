namespace JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Models;

/// <summary>
/// 资源基类
/// </summary>
public abstract class Resource
{
    /// <summary>
    /// 资源名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 资源类型
    /// </summary>
    public abstract ResourceType Type { get; }

    /// <summary>
    /// 资源状态
    /// </summary>
    public ResourceState State { get; internal set; } = ResourceState.Pending;

    /// <summary>
    /// 环境变量
    /// </summary>
    public Dictionary<string, string> Environment { get; } = new();

    /// <summary>
    /// 依赖的资源
    /// </summary>
    public List<Resource> Dependencies { get; } = [];

    protected Resource(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }
}

/// <summary>
/// 资源类型
/// </summary>
public enum ResourceType
{
    /// <summary>
    /// .NET 项目
    /// </summary>
    Project,

    /// <summary>
    /// 可执行程序
    /// </summary>
    Executable,

    /// <summary>
    /// 容器
    /// </summary>
    Container,

    /// <summary>
    /// 外部服务引用
    /// </summary>
    ExternalService,

    /// <summary>
    /// 前端项目
    /// </summary>
    Frontend,
}

/// <summary>
/// 资源状态
/// </summary>
public enum ResourceState
{
    /// <summary>
    /// 等待中
    /// </summary>
    Pending,

    /// <summary>
    /// 启动中
    /// </summary>
    Starting,

    /// <summary>
    /// 运行中
    /// </summary>
    Running,

    /// <summary>
    /// 停止中
    /// </summary>
    Stopping,

    /// <summary>
    /// 已停止
    /// </summary>
    Stopped,

    /// <summary>
    /// 失败
    /// </summary>
    Failed,
}
