using JZVerse.MicroHuaxia.Saga.Abstractions;
using JZVerse.MicroHuaxia.Saga.Core.BackgroundServices;
using JZVerse.MicroHuaxia.Saga.Core.Communication;

namespace JZVerse.MicroHuaxia.Saga.AspNetCore;

/// <summary>
/// Saga 存储类型
/// </summary>
public enum SagaStorageType
{
    /// <summary>
    /// 内存存储
    /// </summary>
    Memory,
    
    /// <summary>
    /// 文件日志存储
    /// </summary>
    FileLog
}

/// <summary>
/// Saga 通信模式
/// </summary>
public enum SagaCommunicationMode
{
    /// <summary>
    /// 本地调用（进程内）
    /// </summary>
    Local,
    
    /// <summary>
    /// HTTP 调用
    /// </summary>
    Http,
    
    /// <summary>
    /// 消息队列
    /// </summary>
    Mq
}

/// <summary>
/// Saga 配置选项
/// </summary>
public sealed class SagaOptions
{
    /// <summary>
    /// Saga 执行模式（协调式/编排式）
    /// </summary>
    public SagaMode Mode { get; set; } = SagaMode.Orchestration;
    
    /// <summary>
    /// 存储类型
    /// </summary>
    public SagaStorageType Storage { get; set; } = SagaStorageType.Memory;
    
    /// <summary>
    /// 通信模式（协调式下生效）
    /// </summary>
    public SagaCommunicationMode CommunicationMode { get; set; } = SagaCommunicationMode.Local;
    
    /// <summary>
    /// 编排式配置（Mode = Choreography 时生效）
    /// </summary>
    public ChoreographyOptions ChoreographyOptions { get; set; } = new();
    
    /// <summary>
    /// 是否启用超时检测
    /// </summary>
    public bool EnableTimeoutDetection { get; set; } = true;
    
    /// <summary>
    /// 是否启用故障恢复
    /// </summary>
    public bool EnableRecovery { get; set; } = true;
    
    /// <summary>
    /// 超时检测配置
    /// </summary>
    public SagaTimeoutOptions TimeoutOptions { get; set; } = new();
    
    /// <summary>
    /// 恢复服务配置
    /// </summary>
    public SagaRecoveryOptions RecoveryOptions { get; set; } = new();
    
    /// <summary>
    /// HTTP 通信配置（当 CommunicationMode = Http 时生效）
    /// </summary>
    public HttpSagaAdapterOptions? HttpOptions { get; set; }
    
    /// <summary>
    /// MQ 通信配置（当 CommunicationMode = Mq 时生效）
    /// </summary>
    public MqSagaAdapterOptions? MqOptions { get; set; }
}

/// <summary>
/// 编排式 Saga 配置选项
/// </summary>
public sealed class ChoreographyOptions
{
    /// <summary>
    /// 事件总线类型
    /// </summary>
    public ChoreographyEventBusType EventBusType { get; set; } = ChoreographyEventBusType.InMemory;
    
    /// <summary>
    /// 是否启用协调器（追踪 Saga 进度）
    /// </summary>
    public bool EnableCoordinator { get; set; } = true;
}

/// <summary>
/// 编排式事件总线类型
/// </summary>
public enum ChoreographyEventBusType
{
    /// <summary>
    /// 内存事件总线（进程内）
    /// </summary>
    InMemory,
    
    /// <summary>
    /// 消息队列事件总线（跨进程）
    /// </summary>
    MessageQueue
}
