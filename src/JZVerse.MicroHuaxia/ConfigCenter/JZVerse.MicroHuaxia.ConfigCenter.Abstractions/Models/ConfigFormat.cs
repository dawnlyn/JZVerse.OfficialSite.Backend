namespace JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

/// <summary>
/// 配置格式
/// </summary>
public enum ConfigFormat
{
    /// <summary>
    /// JSON 格式
    /// </summary>
    JSON = 0,

    /// <summary>
    /// YAML 格式
    /// </summary>
    YAML = 1,

    /// <summary>
    /// XML 格式
    /// </summary>
    XML = 2,

    /// <summary>
    /// Properties 格式（key=value）
    /// </summary>
    Properties = 3,

    /// <summary>
    /// TOML 格式
    /// </summary>
    TOML = 4,
}
