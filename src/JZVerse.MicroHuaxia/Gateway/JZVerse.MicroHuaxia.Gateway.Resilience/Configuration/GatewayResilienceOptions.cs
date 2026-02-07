using JZVerse.MicroHuaxia.Gateway.Resilience.Abstractions;

namespace JZVerse.MicroHuaxia.Gateway.Resilience.Configuration;

/// <summary>
/// Gateway 弹性能力全局配置
/// </summary>
public sealed class GatewayResilienceOptions
{
    /// <summary>
    /// 默认舱壁配置
    /// </summary>
    public BulkheadOptions DefaultBulkhead { get; set; } = new();

    /// <summary>
    /// 是否启用全局弹性能力
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 默认超时时间
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 是否在降级时记录详细日志
    /// </summary>
    public bool VerboseFallbackLogging { get; set; } = false;
}
