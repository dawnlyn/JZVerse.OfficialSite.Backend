namespace JZVerse.MicroHuaxia.Dashboard.Abstractions.Models;

/// <summary>
/// 配置项信息
/// </summary>
public class ConfigItem
{
    /// <summary>
    /// 配置项 ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 配置键
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 配置值
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// 命名空间 ID
    /// </summary>
    public string NamespaceId { get; set; } = string.Empty;

    /// <summary>
    /// 环境 ID
    /// </summary>
    public string EnvironmentId { get; set; } = string.Empty;

    /// <summary>
    /// 值类型
    /// </summary>
    public ConfigValueType ValueType { get; set; } = ConfigValueType.String;

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 版本号
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 命名空间信息
/// </summary>
public class ConfigNamespace
{
    /// <summary>
    /// 命名空间 ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 命名空间名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 配置项数量
    /// </summary>
    public int ItemCount { get; set; }
}

/// <summary>
/// 配置值类型
/// </summary>
public enum ConfigValueType
{
    /// <summary>
    /// 字符串
    /// </summary>
    String,

    /// <summary>
    /// JSON
    /// </summary>
    Json,

    /// <summary>
    /// YAML
    /// </summary>
    Yaml,

    /// <summary>
    /// 数字
    /// </summary>
    Number,

    /// <summary>
    /// 布尔值
    /// </summary>
    Boolean
}
