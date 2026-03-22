namespace JZVerse.MicroHuaxia.Security;

/// <summary>
/// 身份类型枚举
/// </summary>
public enum IdentityType
{
    /// <summary>
    /// 未知类型
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 服务身份
    /// </summary>
    Service = 1,

    /// <summary>
    /// 用户身份
    /// </summary>
    User = 2,

    /// <summary>
    /// 工作负载身份
    /// </summary>
    Workload = 3,

    /// <summary>
    /// 外部客户端身份
    /// </summary>
    ExternalClient = 4,

    /// <summary>
    /// 系统身份
    /// </summary>
    System = 5
}
