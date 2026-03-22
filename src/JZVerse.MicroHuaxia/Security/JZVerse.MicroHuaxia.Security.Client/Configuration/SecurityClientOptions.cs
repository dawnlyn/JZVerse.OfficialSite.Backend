namespace JZVerse.MicroHuaxia.Security.Configuration;

/// <summary>
/// 安全客户端配置选项
/// </summary>
public class SecurityClientOptions
{
    /// <summary>
    /// 安全平台服务地址
    /// </summary>
    public string ServerAddress { get; set; } = "http://localhost:8443";

    /// <summary>
    /// 服务身份标识
    /// </summary>
    public string ServiceIdentity { get; set; } = null!;

    /// <summary>
    /// 命名空间
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// 令牌缓存时间（分钟）
    /// </summary>
    public int TokenCacheMinutes { get; set; } = 55;

    /// <summary>
    /// 请求超时时间（秒）
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 是否启用 mTLS
    /// </summary>
    public bool EnableMutualTls { get; set; } = false;

    /// <summary>
    /// 客户端证书路径
    /// </summary>
    public string? ClientCertificatePath { get; set; }

    /// <summary>
    /// 客户端私钥路径
    /// </summary>
    public string? ClientPrivateKeyPath { get; set; }
}
