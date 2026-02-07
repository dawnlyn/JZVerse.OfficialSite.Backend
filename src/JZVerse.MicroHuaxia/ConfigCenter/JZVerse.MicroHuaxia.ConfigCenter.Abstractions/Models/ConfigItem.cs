namespace JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

/// <summary>
/// 配置项实体
/// </summary>
public sealed record ConfigItem
{
    /// <summary>
    /// 配置项唯一标识
    /// </summary>
    public required string ItemId { get; init; }

    /// <summary>
    /// 配置键（支持层级：database.connection.host）
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// 配置值
    /// </summary>
    public required string Value { get; set; }

    /// <summary>
    /// 值类型
    /// </summary>
    public ConfigValueType ValueType { get; init; } = ConfigValueType.String;

    /// <summary>
    /// 所属命名空间 ID
    /// </summary>
    public required string NamespaceId { get; init; }

    /// <summary>
    /// 所属环境 ID
    /// </summary>
    public required string EnvironmentId { get; init; }

    /// <summary>
    /// 版本号（递增）
    /// </summary>
    public long Version { get; set; } = 1;

    /// <summary>
    /// 注释说明
    /// </summary>
    public string? Comment { get; init; }

    /// <summary>
    /// 是否为敏感配置
    /// </summary>
    public bool IsSecret { get; init; }

    /// <summary>
    /// 是否必填
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 创建人
    /// </summary>
    public string? CreatedBy { get; init; }

    /// <summary>
    /// 修改人
    /// </summary>
    public string? UpdatedBy { get; set; }
}
