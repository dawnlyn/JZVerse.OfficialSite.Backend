namespace JZVerse.Business.Infrastructure.Encryption;

/// <summary>
/// SM2 加密配置选项
/// </summary>
public sealed class Sm2Options
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Encryption:Sm2";

    /// <summary>
    /// 是否启用 SM2 加密
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 公钥（Base64 编码，格式：04 + X坐标 + Y坐标）
    /// </summary>
    public string PublicKey { get; set; } = "";

    /// <summary>
    /// 私钥（Base64 编码，32字节）
    /// </summary>
    public string PrivateKey { get; set; } = "";
}
