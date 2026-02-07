using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.MessageQueue.Core.Delay;

/// <summary>
/// 时间轮配置
/// </summary>
public sealed class TimeWheelOptions
{
    /// <summary>
    /// 每个槽位的时间跨度（毫秒）- 时间轮精度
    /// </summary>
    public int TickDurationMs { get; set; } = 100;
    
    /// <summary>
    /// 槽位数量（必须是2的幂）
    /// </summary>
    public int WheelSize { get; set; } = 512;
    
    /// <summary>
    /// 最大延迟时间（秒）
    /// </summary>
    public int MaxDelaySeconds { get; set; } = 86400 * 7; // 7天
}

/// <summary>
/// 层级时间轮实现
/// 
/// 设计原理：
/// - 使用多层时间轮实现大范围延迟调度
/// - 第一层：毫秒级精度（100ms tick, 512 slots = 51.2秒）
/// - 第二层：秒级精度（51.2秒 tick, 512 slots = 7.28小时）
/// - 第三层：分钟级精度（7.28小时 tick, 512 slots = 155天）
/// - 当高层时间轮的任务到期时，会降级到低层时间轮
/// </summary>
public sealed class HierarchicalTimeWheel : IDisposable
{
    private readonly ILogger<HierarchicalTimeWheel> _logger;
    private readonly TimeWheelOptions _options;
    private readonly TimeWheelSlot[] _slots;
    private readonly HierarchicalTimeWheel? _overflowWheel;
    private readonly long _tickDurationMs;
    private readonly int _wheelSize;
    private readonly int _mask;
    private readonly long _interval; // 整个时间轮覆盖的时间范围
    
    private long _currentTime;
    private int _currentSlot;
    private bool _disposed;

    /// <summary>
    /// 每个槽位的时间跨度（毫秒）
    /// </summary>
    public long TickDuration => _tickDurationMs;
    
    /// <summary>
    /// 时间轮覆盖的时间范围（毫秒）
    /// </summary>
    public long Interval => _interval;

    /// <summary>
    /// 创建第一层时间轮
    /// </summary>
    public HierarchicalTimeWheel(ILogger<HierarchicalTimeWheel> logger, TimeWheelOptions options)
        : this(logger, options, options.TickDurationMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), null)
    {
    }

    /// <summary>
    /// 内部构造函数（用于创建上层时间轮）
    /// </summary>
    private HierarchicalTimeWheel(
        ILogger<HierarchicalTimeWheel> logger,
        TimeWheelOptions options,
        long tickDurationMs,
        long startMs,
        HierarchicalTimeWheel? overflowWheel)
    {
        _logger = logger;
        _options = options;
        _tickDurationMs = tickDurationMs;
        _wheelSize = options.WheelSize;
        _mask = _wheelSize - 1;
        _interval = tickDurationMs * _wheelSize;
        _currentTime = startMs - (startMs % tickDurationMs);
        _currentSlot = 0;
        
        _slots = new TimeWheelSlot[_wheelSize];
        for (int i = 0; i < _wheelSize; i++)
        {
            _slots[i] = new TimeWheelSlot();
        }
        
        // 如果还需要支持更长的延迟，创建上层时间轮
        var maxDelayMs = options.MaxDelaySeconds * 1000L;
        if (_interval < maxDelayMs && tickDurationMs < maxDelayMs)
        {
            var nextTickDuration = _interval; // 上层的tick duration = 当前层的总时间跨度
            if (nextTickDuration < maxDelayMs)
            {
                _overflowWheel = new HierarchicalTimeWheel(logger, options, nextTickDuration, startMs, this);
            }
        }
    }

    /// <summary>
    /// 添加延迟任务
    /// </summary>
    /// <param name="entry">延迟消息条目</param>
    /// <returns>是否成功添加</returns>
    public bool Add(DelayedMessageEntry entry)
    {
        if (_disposed) return false;
        
        var deliveryMs = entry.DeliveryTime.ToUnixTimeMilliseconds();
        var delayMs = deliveryMs - _currentTime;
        
        if (delayMs < _tickDurationMs)
        {
            // 已到期或即将到期，不添加到时间轮
            return false;
        }
        
        if (delayMs < _interval)
        {
            // 在当前时间轮范围内
            // 计算相对于当前槽位的偏移
            var ticksFromNow = (int)(delayMs / _tickDurationMs);
            var slotIndex = (_currentSlot + ticksFromNow) & _mask;
            entry.RemainingRounds = 0; // 在当前时间轮范围内，不需要额外轮次
            
            _slots[slotIndex].Add(entry);
            _logger.LogDebug("Added delayed message {MessageId} to slot {Slot} (current: {Current}), delay {Delay}ms",
                entry.Message.MessageId, slotIndex, _currentSlot, delayMs);
            return true;
        }
        
        // 超出当前时间轮范围，添加到上层时间轮
        if (_overflowWheel != null)
        {
            return _overflowWheel.Add(entry);
        }
        
        // 没有上层时间轮，使用轮次计数
        var ticksFromNowLong = delayMs / _tickDurationMs;
        var targetSlot = (_currentSlot + (int)(ticksFromNowLong % _wheelSize)) & _mask;
        entry.RemainingRounds = (int)(ticksFromNowLong / _wheelSize);
        _slots[targetSlot].Add(entry);
        
        _logger.LogDebug("Message {MessageId} delay {Delay}ms, added to slot {Slot} with {Rounds} rounds",
            entry.Message.MessageId, delayMs, targetSlot, entry.RemainingRounds);
        return true;
    }

    /// <summary>
    /// 取消延迟消息
    /// </summary>
    public bool Cancel(string messageId)
    {
        // 遍历所有槽位查找
        for (int i = 0; i < _wheelSize; i++)
        {
            if (_slots[i].Remove(messageId))
            {
                return true;
            }
        }
        
        // 尝试在上层时间轮中取消
        return _overflowWheel?.Cancel(messageId) ?? false;
    }

    /// <summary>
    /// 推进时间轮（tick）
    /// </summary>
    /// <param name="timestamp">当前时间戳（毫秒）</param>
    /// <returns>到期的消息条目</returns>
    public List<DelayedMessageEntry> AdvanceClock(long timestamp)
    {
        var expiredEntries = new List<DelayedMessageEntry>();
        
        if (timestamp < _currentTime + _tickDurationMs)
        {
            // 时间还未到下一个tick
            return expiredEntries;
        }
        
        // 推进时间
        while (_currentTime + _tickDurationMs <= timestamp)
        {
            _currentTime += _tickDurationMs;
            _currentSlot = (_currentSlot + 1) & _mask;
            
            // 获取当前槽位的所有条目
            var entries = _slots[_currentSlot].FlushAll();
            
            foreach (var entry in entries)
            {
                if (entry.IsCancelled)
                {
                    continue;
                }
                
                if (entry.RemainingRounds > 0)
                {
                    // 还有剩余轮次，重新加入
                    entry.RemainingRounds--;
                    _slots[_currentSlot].Add(entry);
                }
                else
                {
                    // 已到期
                    expiredEntries.Add(entry);
                }
            }
        }
        
        // 推进上层时间轮并处理降级任务
        if (_overflowWheel != null)
        {
            var overflowExpired = _overflowWheel.AdvanceClock(timestamp);
            foreach (var entry in overflowExpired)
            {
                if (!entry.IsCancelled)
                {
                    // 降级到当前时间轮
                    if (!Add(entry))
                    {
                        // 如果添加失败（已到期），直接加入到期列表
                        expiredEntries.Add(entry);
                    }
                }
            }
        }
        
        return expiredEntries;
    }

    /// <summary>
    /// 获取所有待处理的消息数量
    /// </summary>
    public long GetPendingCount()
    {
        long count = 0;
        for (int i = 0; i < _wheelSize; i++)
        {
            count += _slots[i].Count;
        }
        
        if (_overflowWheel != null)
        {
            count += _overflowWheel.GetPendingCount();
        }
        
        return count;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _overflowWheel?.Dispose();
    }
}
