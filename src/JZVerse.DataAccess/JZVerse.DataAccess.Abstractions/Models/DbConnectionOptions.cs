namespace JZVerse.DataAccess.Abstractions.Models;

/// <summary>
/// 数据库连接配置选项
/// </summary>
public class DbConnectionOptions
{
    /// <summary>
    /// 数据库类型
    /// </summary>
    public DatabaseType DatabaseType { get; set; }

    /// <summary>
    /// 连接字符串
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// 最大连接池大小
    /// </summary>
    public int MaxPoolSize { get; set; } = 100;

    /// <summary>
    /// 最小连接池大小
    /// </summary>
    public int MinPoolSize { get; set; } = 10;

    /// <summary>
    /// 连接超时时间（秒）
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 命令超时时间（秒）
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 是否启用连接池
    /// </summary>
    public bool EnablePooling { get; set; } = true;
}
