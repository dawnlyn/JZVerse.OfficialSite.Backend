namespace JZVerse.MicroHuaxia.Saga.Server.Models;

/// <summary>
/// Saga 启动请求
/// </summary>
public sealed class SagaStartRequest
{
    /// <summary>
    /// Saga 定义 ID
    /// </summary>
    public required string SagaId { get; init; }

    /// <summary>
    /// 初始上下文数据
    /// </summary>
    public Dictionary<string, object>? InitialData { get; init; }

    /// <summary>
    /// 超时时间（秒），为 null 使用默认值
    /// </summary>
    public int? TimeoutSeconds { get; init; }
}
