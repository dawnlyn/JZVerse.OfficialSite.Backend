namespace JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

/// <summary>
/// 客户端信息（用于灰度匹配）
/// </summary>
public sealed class ClientInfo
{
    /// <summary>
    /// 客户端 ID
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// 客户端 IP 地址
    /// </summary>
    public string? IpAddress { get; init; }

    /// <summary>
    /// 客户端标签
    /// </summary>
    public List<string> Tags { get; init; } = [];

    /// <summary>
    /// 附加属性
    /// </summary>
    public Dictionary<string, string> Properties { get; init; } = new();
}
