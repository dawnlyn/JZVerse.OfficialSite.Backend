namespace JZVerse.MicroHuaxia.Security;

/// <summary>
/// 审计级别枚举
/// </summary>
public enum AuditLevel
{
    /// <summary>
    /// 调试级别
    /// </summary>
    Debug = 0,

    /// <summary>
    /// 普通信息
    /// </summary>
    Info = 1,

    /// <summary>
    /// 重要操作
    /// </summary>
    Important = 2,

    /// <summary>
    /// 敏感操作
    /// </summary>
    Sensitive = 3,

    /// <summary>
    /// 安全相关
    /// </summary>
    Security = 4
}
