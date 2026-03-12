namespace JZVerse.Business.Dashboard.Database;

#region System Config Entities

/// <summary>
/// 系统配置实体
/// </summary>
public sealed record SystemConfigEntity
{
    /// <summary>
    /// 配置唯一标识
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 配置键名
    /// </summary>
    public string ConfigKey { get; init; } = "";

    /// <summary>
    /// 配置值
    /// </summary>
    public string ConfigValue { get; init; } = "";

    /// <summary>
    /// 配置说明
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 配置分类
    /// </summary>
    public ConfigCategory Category { get; init; }

    /// <summary>
    /// 是否公开（前端可访问）
    /// </summary>
    public bool IsPublic { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// 配置分类
/// </summary>
public enum ConfigCategory
{
    /// <summary>
    /// 基础配置
    /// </summary>
    Basic = 1,

    /// <summary>
    /// 模块配置
    /// </summary>
    Module = 2,

    /// <summary>
    /// 接口配置
    /// </summary>
    Api = 3,

    /// <summary>
    /// 存储配置
    /// </summary>
    Storage = 4,

    /// <summary>
    /// 安全配置
    /// </summary>
    Security = 5
}

#endregion

#region Menu & Permission Entities

/// <summary>
/// 菜单实体
/// </summary>
public sealed record MenuEntity
{
    /// <summary>
    /// 菜单唯一标识
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 父级菜单 ID（顶级菜单为 null）
    /// </summary>
    public Guid? ParentId { get; init; }

    /// <summary>
    /// 菜单名称
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// 菜单图标
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>
    /// 路由路径
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// 前端组件路径
    /// </summary>
    public string? Component { get; init; }

    /// <summary>
    /// 所需权限标识
    /// </summary>
    public string? Permission { get; init; }

    /// <summary>
    /// 排序序号
    /// </summary>
    public int SortOrder { get; init; }

    /// <summary>
    /// 是否在菜单中显示
    /// </summary>
    public bool IsVisible { get; init; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// 角色实体
/// </summary>
public sealed record RoleEntity
{
    /// <summary>
    /// 角色唯一标识
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 角色编码（用于程序判断）
    /// </summary>
    public string Code { get; init; } = "";

    /// <summary>
    /// 角色名称
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// 角色描述
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 是否为系统内置角色
    /// </summary>
    public bool IsSystem { get; init; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// 角色菜单关联
/// </summary>
public sealed record RoleMenuEntity
{
    /// <summary>
    /// 角色 ID
    /// </summary>
    public Guid RoleId { get; init; }

    /// <summary>
    /// 菜单 ID
    /// </summary>
    public Guid MenuId { get; init; }

    /// <summary>
    /// 关联创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }
}

#endregion

#region Log Entities

/// <summary>
/// 操作日志实体
/// </summary>
public sealed record OperationLogEntity
{
    /// <summary>
    /// 日志唯一标识
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 操作人 ID
    /// </summary>
    public Guid? OperatorId { get; init; }

    /// <summary>
    /// 操作人名称
    /// </summary>
    public string? OperatorName { get; init; }

    /// <summary>
    /// 操作所属模块
    /// </summary>
    public string Module { get; init; } = "";

    /// <summary>
    /// 操作动作
    /// </summary>
    public string Action { get; init; } = "";

    /// <summary>
    /// 操作描述
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// HTTP 请求方法
    /// </summary>
    public string? RequestMethod { get; init; }

    /// <summary>
    /// 请求路径
    /// </summary>
    public string? RequestPath { get; init; }

    /// <summary>
    /// 请求体内容
    /// </summary>
    public string? RequestBody { get; init; }

    /// <summary>
    /// 响应体内容
    /// </summary>
    public string? ResponseBody { get; init; }

    /// <summary>
    /// 客户端 IP 地址
    /// </summary>
    public string? ClientIp { get; init; }

    /// <summary>
    /// 客户端 User-Agent
    /// </summary>
    public string? UserAgent { get; init; }

    /// <summary>
    /// HTTP 响应状态码
    /// </summary>
    public int StatusCode { get; init; }

    /// <summary>
    /// 请求执行耗时（毫秒）
    /// </summary>
    public long ExecutionTime { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// 异常日志实体
/// </summary>
public sealed record ExceptionLogEntity
{
    /// <summary>
    /// 日志唯一标识
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 异常所属模块
    /// </summary>
    public string Module { get; init; } = "";

    /// <summary>
    /// 异常类型
    /// </summary>
    public string ExceptionType { get; init; } = "";

    /// <summary>
    /// 异常消息
    /// </summary>
    public string Message { get; init; } = "";

    /// <summary>
    /// 异常堆栈信息
    /// </summary>
    public string? StackTrace { get; init; }

    /// <summary>
    /// 请求路径
    /// </summary>
    public string? RequestPath { get; init; }

    /// <summary>
    /// 请求体内容
    /// </summary>
    public string? RequestBody { get; init; }

    /// <summary>
    /// 客户端 IP 地址
    /// </summary>
    public string? ClientIp { get; init; }

    /// <summary>
    /// 处理状态
    /// </summary>
    public ExceptionLogStatus Status { get; init; }

    /// <summary>
    /// 解决方案说明
    /// </summary>
    public string? Solution { get; init; }

    /// <summary>
    /// 处理人 ID
    /// </summary>
    public Guid? HandlerId { get; init; }

    /// <summary>
    /// 处理时间
    /// </summary>
    public DateTime? HandledAt { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// 异常日志状态
/// </summary>
public enum ExceptionLogStatus
{
    /// <summary>
    /// 待处理
    /// </summary>
    Pending = 1,

    /// <summary>
    /// 处理中
    /// </summary>
    Processing = 2,

    /// <summary>
    /// 已处理
    /// </summary>
    Resolved = 3,

    /// <summary>
    /// 已忽略
    /// </summary>
    Ignored = 4
}

/// <summary>
/// 登录日志实体
/// </summary>
public sealed record LoginLogEntity
{
    /// <summary>
    /// 日志唯一标识
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 登录用户 ID
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// 登录用户名
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// 登录类型
    /// </summary>
    public LoginLogType LoginType { get; init; }

    /// <summary>
    /// 客户端 IP 地址
    /// </summary>
    public string? ClientIp { get; init; }

    /// <summary>
    /// IP 归属地
    /// </summary>
    public string? Location { get; init; }

    /// <summary>
    /// 客户端 User-Agent
    /// </summary>
    public string? UserAgent { get; init; }

    /// <summary>
    /// 设备类型
    /// </summary>
    public string? DeviceType { get; init; }

    /// <summary>
    /// 浏览器类型
    /// </summary>
    public string? Browser { get; init; }

    /// <summary>
    /// 操作系统
    /// </summary>
    public string? Os { get; init; }

    /// <summary>
    /// 是否登录成功
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 登录失败原因
    /// </summary>
    public string? FailReason { get; init; }

    /// <summary>
    /// 是否异常登录
    /// </summary>
    public bool IsAbnormal { get; init; }

    /// <summary>
    /// 异常登录原因
    /// </summary>
    public string? AbnormalReason { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// 登录类型
/// </summary>
public enum LoginLogType
{
    /// <summary>
    /// 管理员登录
    /// </summary>
    Admin = 1,

    /// <summary>
    /// 用户登录
    /// </summary>
    User = 2
}

#endregion

#region Notification Entities

/// <summary>
/// 待办提醒实体
/// </summary>
public sealed record TodoNotificationEntity
{
    /// <summary>
    /// 待办唯一标识
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 待办标题
    /// </summary>
    public string Title { get; init; } = "";

    /// <summary>
    /// 待办内容
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// 待办类型
    /// </summary>
    public TodoNotificationType Type { get; init; }

    /// <summary>
    /// 紧急级别
    /// </summary>
    public TodoNotificationLevel Level { get; init; }

    /// <summary>
    /// 来源模块
    /// </summary>
    public string? SourceModule { get; init; }

    /// <summary>
    /// 来源数据 ID
    /// </summary>
    public string? SourceId { get; init; }

    /// <summary>
    /// 操作跳转 URL
    /// </summary>
    public string? ActionUrl { get; init; }

    /// <summary>
    /// 是否已读
    /// </summary>
    public bool IsRead { get; init; }

    /// <summary>
    /// 已读人 ID
    /// </summary>
    public Guid? ReadBy { get; init; }

    /// <summary>
    /// 已读时间
    /// </summary>
    public DateTime? ReadAt { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// 待办类型
/// </summary>
public enum TodoNotificationType
{
    /// <summary>
    /// 安全预警
    /// </summary>
    SecurityAlert = 1,

    /// <summary>
    /// 存储容量预警
    /// </summary>
    StorageAlert = 2,

    /// <summary>
    /// 异常登录预警
    /// </summary>
    LoginAlert = 3,

    /// <summary>
    /// 系统配置变更
    /// </summary>
    ConfigChange = 4,

    /// <summary>
    /// 内容审核
    /// </summary>
    ContentReview = 5
}

/// <summary>
/// 待办级别
/// </summary>
public enum TodoNotificationLevel
{
    /// <summary>
    /// 普通
    /// </summary>
    Normal = 1,

    /// <summary>
    /// 警告
    /// </summary>
    Warning = 2,

    /// <summary>
    /// 紧急
    /// </summary>
    Urgent = 3
}

#endregion
