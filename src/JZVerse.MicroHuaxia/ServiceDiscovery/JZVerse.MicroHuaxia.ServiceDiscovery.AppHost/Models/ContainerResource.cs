namespace JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Models;

/// <summary>
/// 容器资源
/// </summary>
public sealed class ContainerResource : Resource
{
    /// <summary>
    /// 镜像名称
    /// </summary>
    public string Image { get; }

    /// <summary>
    /// 镜像标签
    /// </summary>
    public string Tag { get; set; } = "latest";

    /// <summary>
    /// 端点配置
    /// </summary>
    public List<EndpointConfig> Endpoints { get; } = [];

    /// <summary>
    /// 卷挂载
    /// </summary>
    public List<VolumeMount> Volumes { get; } = [];

    /// <summary>
    /// 命令行参数
    /// </summary>
    public List<string> Args { get; } = [];

    /// <summary>
    /// 入口点覆盖
    /// </summary>
    public string? Entrypoint { get; set; }

    /// <summary>
    /// 容器名称
    /// </summary>
    public string? ContainerName { get; set; }

    /// <summary>
    /// 是否总是拉取镜像
    /// </summary>
    public bool AlwaysPull { get; set; }

    /// <inheritdoc />
    public override ResourceType Type => ResourceType.Container;

    public ContainerResource(string name, string image)
        : base(name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(image);
        Image = image;
    }

    /// <summary>
    /// 获取完整镜像名称
    /// </summary>
    public string GetFullImageName() => $"{Image}:{Tag}";
}

/// <summary>
/// 卷挂载配置
/// </summary>
public sealed class VolumeMount
{
    /// <summary>
    /// 挂载类型
    /// </summary>
    public VolumeMountType Type { get; set; } = VolumeMountType.Bind;

    /// <summary>
    /// 源路径或卷名称
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// 容器内目标路径
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// 是否只读
    /// </summary>
    public bool ReadOnly { get; set; }
}

/// <summary>
/// 卷挂载类型
/// </summary>
public enum VolumeMountType
{
    /// <summary>
    /// 绑定挂载
    /// </summary>
    Bind,

    /// <summary>
    /// 命名卷
    /// </summary>
    Volume,

    /// <summary>
    /// 临时文件系统
    /// </summary>
    Tmpfs,
}
