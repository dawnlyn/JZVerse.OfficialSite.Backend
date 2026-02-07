namespace JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;

/// <summary>
/// 健康状态枚举
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// 未知（初始状态）
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 健康
    /// </summary>
    Healthy = 1,

    /// <summary>
    /// 不健康
    /// </summary>
    Unhealthy = 2,

    /// <summary>
    /// 降级（部分功能不可用但服务仍可用）
    /// </summary>
    Degraded = 3,

    /// <summary>
    /// 启动中
    /// </summary>
    Starting = 4,

    /// <summary>
    /// 停止中
    /// </summary>
    Stopping = 5,
}

/// <summary>
/// 健康检查结果
/// </summary>
public sealed class HealthCheckResult
{
    /// <summary>
    /// 健康状态
    /// </summary>
    public HealthStatus Status { get; init; }

    /// <summary>
    /// 状态消息
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// 附加数据
    /// </summary>
    public Dictionary<string, object>? Data { get; init; }

    /// <summary>
    /// 检查时间
    /// </summary>
    public DateTimeOffset CheckedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 检查耗时
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// 创建健康结果
    /// </summary>
    public static HealthCheckResult Healthy(string? message = null) =>
        new() { Status = HealthStatus.Healthy, Message = message };

    /// <summary>
    /// 创建不健康结果
    /// </summary>
    public static HealthCheckResult Unhealthy(string? message = null) =>
        new() { Status = HealthStatus.Unhealthy, Message = message };

    /// <summary>
    /// 创建降级结果
    /// </summary>
    public static HealthCheckResult Degraded(string? message = null) =>
        new() { Status = HealthStatus.Degraded, Message = message };
}
