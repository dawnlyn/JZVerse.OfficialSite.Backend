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

/// <summary>
/// 服务运行时配置信息
/// </summary>
public class ServiceConfigInfo
{
    /// <summary>
    /// 服务名称
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// 服务显示名称
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 是否成功获取配置
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// 错误信息（获取失败时）
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 配置项列表（扁平化 key-value）
    /// </summary>
    public List<ServiceConfigEntry> Entries { get; set; } = [];
}

/// <summary>
/// 服务配置项
/// </summary>
public class ServiceConfigEntry
{
    /// <summary>
    /// 配置键（支持层级，如 "ListenPorts.Http"）
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 配置值
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// 值类型
    /// </summary>
    public string ValueType { get; set; } = "String";
}

/// <summary>
/// 灰度发布信息
/// </summary>
public class GrayReleaseInfo
{
    /// <summary>
    /// 发布 ID
    /// </summary>
    public string ReleaseId { get; set; } = string.Empty;

    /// <summary>
    /// 发布名称
    /// </summary>
    public string ReleaseName { get; set; } = string.Empty;

    /// <summary>
    /// 命名空间 ID
    /// </summary>
    public string NamespaceId { get; set; } = string.Empty;

    /// <summary>
    /// 环境 ID
    /// </summary>
    public string EnvironmentId { get; set; } = string.Empty;

    /// <summary>
    /// 策略（Manual/Automatic/Canary）
    /// </summary>
    public string Strategy { get; set; } = string.Empty;

    /// <summary>
    /// 状态（Draft/InProgress/Completed/Rollback/Cancelled）
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 发布百分比 0-100
    /// </summary>
    public int RolloutPercentage { get; set; }

    /// <summary>
    /// 灰度规则列表
    /// </summary>
    public List<GrayRuleInfo> TargetRules { get; set; } = [];

    /// <summary>
    /// 配置快照
    /// </summary>
    public Dictionary<string, string> ConfigSnapshot { get; set; } = new();

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// 完成时间
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// 创建人
    /// </summary>
    public string? CreatedBy { get; set; }
}

/// <summary>
/// 灰度规则信息
/// </summary>
public class GrayRuleInfo
{
    /// <summary>
    /// 规则类型（IP/Tag/ClientId/Percentage）
    /// </summary>
    public string RuleType { get; set; } = string.Empty;

    /// <summary>
    /// 匹配模式
    /// </summary>
    public string MatchPattern { get; set; } = string.Empty;

    /// <summary>
    /// 优先级（数字越小优先级越高）
    /// </summary>
    public int Priority { get; set; }
}

/// <summary>
/// 创建灰度发布输入
/// </summary>
public class CreateGrayReleaseInput
{
    /// <summary>
    /// 发布名称
    /// </summary>
    public string ReleaseName { get; set; } = string.Empty;

    /// <summary>
    /// 命名空间 ID
    /// </summary>
    public string NamespaceId { get; set; } = string.Empty;

    /// <summary>
    /// 环境 ID
    /// </summary>
    public string EnvironmentId { get; set; } = string.Empty;

    /// <summary>
    /// 策略（Manual/Automatic/Canary）
    /// </summary>
    public string Strategy { get; set; } = "Manual";

    /// <summary>
    /// 发布百分比 0-100
    /// </summary>
    public int RolloutPercentage { get; set; }

    /// <summary>
    /// 规则列表
    /// </summary>
    public List<GrayRuleInfo> Rules { get; set; } = [];
}

/// <summary>
/// 客户端匹配测试输入
/// </summary>
public class ClientMatchTestInput
{
    /// <summary>
    /// 客户端 ID
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// IP 地址
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// 标签（逗号分隔）
    /// </summary>
    public string? Tags { get; set; }
}

/// <summary>
/// 客户端匹配测试结果
/// </summary>
public class ClientMatchTestResult
{
    /// <summary>
    /// 是否匹配
    /// </summary>
    public bool Matches { get; set; }

    /// <summary>
    /// 匹配的发布 ID
    /// </summary>
    public string ReleaseId { get; set; } = string.Empty;
}

/// <summary>
/// 配置版本信息
/// </summary>
public class ConfigVersionInfo
{
    /// <summary>
    /// 版本 ID
    /// </summary>
    public string VersionId { get; set; } = string.Empty;

    /// <summary>
    /// 配置项 ID
    /// </summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary>
    /// 版本号
    /// </summary>
    public long Version { get; set; }

    /// <summary>
    /// 旧值
    /// </summary>
    public string? OldValue { get; set; }

    /// <summary>
    /// 新值
    /// </summary>
    public string NewValue { get; set; } = string.Empty;

    /// <summary>
    /// 变更类型（Created/Updated/Deleted/Rollback）
    /// </summary>
    public string ChangeType { get; set; } = string.Empty;

    /// <summary>
    /// 变更原因
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 创建人
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// 回滚来源版本
    /// </summary>
    public long? RollbackFromVersion { get; set; }
}

/// <summary>
/// 配置迁移定义（描述哪些服务的哪些配置节应迁移到 ConfigCenter）
/// </summary>
public class ConfigMigrationProfile
{
    public string ServiceName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string TargetNamespace { get; set; } = string.Empty;
    public string TargetEnvironment { get; set; } = "Production";

    /// <summary>
    /// Bootstrap 前缀列表（匹配的配置保留在本地 appsettings.json，不迁移）
    /// </summary>
    public List<string> BootstrapPrefixes { get; set; } = [];
}

/// <summary>
/// 配置迁移预览项
/// </summary>
public class ConfigMigrationPreviewItem
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string ValueType { get; set; } = "String";
    public bool IsBootstrap { get; set; }
}

/// <summary>
/// 配置迁移结果
/// </summary>
public class ConfigMigrationResult
{
    public string ServiceName { get; set; } = string.Empty;
    public string TargetNamespace { get; set; } = string.Empty;
    public int TotalItems { get; set; }
    public int MigratedItems { get; set; }
    public int SkippedBootstrap { get; set; }
    public int FailedItems { get; set; }
    public List<string> Errors { get; set; } = [];
    public bool Success => FailedItems == 0 && Errors.Count == 0;
}
