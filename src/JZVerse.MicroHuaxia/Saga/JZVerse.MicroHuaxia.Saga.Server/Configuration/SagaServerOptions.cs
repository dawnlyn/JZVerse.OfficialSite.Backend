using JZVerse.MicroHuaxia.Saga.AspNetCore;

namespace JZVerse.MicroHuaxia.Saga.Server.Configuration;

/// <summary>
/// Saga 服务端配置选项
/// </summary>
public sealed class SagaServerOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Saga:Server";

    /// <summary>
    /// 服务名称
    /// </summary>
    public string ServiceName { get; set; } = "saga-server";

    /// <summary>
    /// 存储类型
    /// </summary>
    public SagaStorageType Storage { get; set; } = SagaStorageType.Memory;

    /// <summary>
    /// 通信模式
    /// </summary>
    public SagaCommunicationMode CommunicationMode { get; set; } = SagaCommunicationMode.Http;

    /// <summary>
    /// 是否启用超时检测
    /// </summary>
    public bool EnableTimeoutDetection { get; set; } = true;

    /// <summary>
    /// 是否启用故障恢复
    /// </summary>
    public bool EnableRecovery { get; set; } = true;

    /// <summary>
    /// 默认超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// 恢复检查间隔（秒）
    /// </summary>
    public int RecoveryIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// 最大并发 Saga 实例数
    /// </summary>
    public int MaxConcurrentSagas { get; set; } = 1000;
}
