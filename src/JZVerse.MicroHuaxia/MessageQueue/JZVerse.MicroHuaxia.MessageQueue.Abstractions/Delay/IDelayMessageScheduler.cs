using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;

namespace JZVerse.MicroHuaxia.MessageQueue.Abstractions.Delay;

/// <summary>
/// 延迟级别（预设）
/// </summary>
public enum DelayLevel
{
    /// <summary>
    /// 1秒
    /// </summary>
    Second1 = 1,

    /// <summary>
    /// 5秒
    /// </summary>
    Second5 = 2,

    /// <summary>
    /// 10秒
    /// </summary>
    Second10 = 3,

    /// <summary>
    /// 30秒
    /// </summary>
    Second30 = 4,

    /// <summary>
    /// 1分钟
    /// </summary>
    Minute1 = 5,

    /// <summary>
    /// 2分钟
    /// </summary>
    Minute2 = 6,

    /// <summary>
    /// 5分钟
    /// </summary>
    Minute5 = 7,

    /// <summary>
    /// 10分钟
    /// </summary>
    Minute10 = 8,

    /// <summary>
    /// 30分钟
    /// </summary>
    Minute30 = 9,

    /// <summary>
    /// 1小时
    /// </summary>
    Hour1 = 10,

    /// <summary>
    /// 2小时
    /// </summary>
    Hour2 = 11,

    /// <summary>
    /// 6小时
    /// </summary>
    Hour6 = 12,

    /// <summary>
    /// 12小时
    /// </summary>
    Hour12 = 13,

    /// <summary>
    /// 24小时
    /// </summary>
    Hour24 = 14
}

/// <summary>
/// 延迟级别扩展方法
/// </summary>
public static class DelayLevelExtensions
{
    /// <summary>
    /// 获取延迟时间
    /// </summary>
    public static TimeSpan ToTimeSpan(this DelayLevel level) => level switch
    {
        DelayLevel.Second1 => TimeSpan.FromSeconds(1),
        DelayLevel.Second5 => TimeSpan.FromSeconds(5),
        DelayLevel.Second10 => TimeSpan.FromSeconds(10),
        DelayLevel.Second30 => TimeSpan.FromSeconds(30),
        DelayLevel.Minute1 => TimeSpan.FromMinutes(1),
        DelayLevel.Minute2 => TimeSpan.FromMinutes(2),
        DelayLevel.Minute5 => TimeSpan.FromMinutes(5),
        DelayLevel.Minute10 => TimeSpan.FromMinutes(10),
        DelayLevel.Minute30 => TimeSpan.FromMinutes(30),
        DelayLevel.Hour1 => TimeSpan.FromHours(1),
        DelayLevel.Hour2 => TimeSpan.FromHours(2),
        DelayLevel.Hour6 => TimeSpan.FromHours(6),
        DelayLevel.Hour12 => TimeSpan.FromHours(12),
        DelayLevel.Hour24 => TimeSpan.FromHours(24),
        _ => TimeSpan.Zero
    };

    /// <summary>
    /// 从时间间隔获取最接近的延迟级别
    /// </summary>
    public static DelayLevel FromTimeSpan(TimeSpan delay)
    {
        var seconds = delay.TotalSeconds;
        
        if (seconds <= 1) return DelayLevel.Second1;
        if (seconds <= 5) return DelayLevel.Second5;
        if (seconds <= 10) return DelayLevel.Second10;
        if (seconds <= 30) return DelayLevel.Second30;
        if (seconds <= 60) return DelayLevel.Minute1;
        if (seconds <= 120) return DelayLevel.Minute2;
        if (seconds <= 300) return DelayLevel.Minute5;
        if (seconds <= 600) return DelayLevel.Minute10;
        if (seconds <= 1800) return DelayLevel.Minute30;
        if (seconds <= 3600) return DelayLevel.Hour1;
        if (seconds <= 7200) return DelayLevel.Hour2;
        if (seconds <= 21600) return DelayLevel.Hour6;
        if (seconds <= 43200) return DelayLevel.Hour12;
        
        return DelayLevel.Hour24;
    }
}

/// <summary>
/// 延迟消息调度器接口
/// </summary>
public interface IDelayMessageScheduler
{
    /// <summary>
    /// 调度延迟消息
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="delay">延迟时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ScheduleAsync(IMessage message, TimeSpan delay, CancellationToken cancellationToken = default);

    /// <summary>
    /// 调度延迟消息（使用预设级别）
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="level">延迟级别</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ScheduleAsync(IMessage message, DelayLevel level, CancellationToken cancellationToken = default);

    /// <summary>
    /// 调度定时消息
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="deliveryTime">投递时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ScheduleAtAsync(IMessage message, DateTimeOffset deliveryTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取消延迟消息
    /// </summary>
    /// <param name="messageId">消息ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> CancelAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 启动调度器
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止调度器
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 延迟消息存储接口
/// </summary>
public interface IDelayMessageStore
{
    /// <summary>
    /// 添加延迟消息
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="deliveryTime">投递时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(IMessage message, DateTimeOffset deliveryTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取到期的消息
    /// </summary>
    /// <param name="batchSize">批量大小</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<IMessage>> GetDueMessagesAsync(int batchSize = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除消息
    /// </summary>
    /// <param name="messageId">消息ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> RemoveAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取待投递消息数量
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task<long> GetPendingCountAsync(CancellationToken cancellationToken = default);
}
