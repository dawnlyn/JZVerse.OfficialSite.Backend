using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;

namespace JZVerse.MicroHuaxia.MessageQueue.Core.Delay;

/// <summary>
/// 延迟消息条目
/// </summary>
public sealed class DelayedMessageEntry
{
    /// <summary>
    /// 消息
    /// </summary>
    public required IMessage Message { get; init; }
    
    /// <summary>
    /// 预期投递时间
    /// </summary>
    public required DateTimeOffset DeliveryTime { get; init; }
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// 剩余轮次（用于多层时间轮）
    /// </summary>
    public int RemainingRounds { get; set; }
    
    /// <summary>
    /// 是否已取消
    /// </summary>
    public bool IsCancelled { get; set; }
}

/// <summary>
/// 时间轮槽位
/// </summary>
public sealed class TimeWheelSlot
{
    private readonly object _lock = new();
    private readonly LinkedList<DelayedMessageEntry> _entries = new();

    /// <summary>
    /// 添加条目
    /// </summary>
    public void Add(DelayedMessageEntry entry)
    {
        lock (_lock)
        {
            _entries.AddLast(entry);
        }
    }

    /// <summary>
    /// 移除条目
    /// </summary>
    public bool Remove(string messageId)
    {
        lock (_lock)
        {
            var node = _entries.First;
            while (node != null)
            {
                if (node.Value.Message.MessageId == messageId)
                {
                    _entries.Remove(node);
                    return true;
                }
                node = node.Next;
            }
            return false;
        }
    }

    /// <summary>
    /// 获取并清空所有条目
    /// </summary>
    public List<DelayedMessageEntry> FlushAll()
    {
        lock (_lock)
        {
            var result = _entries.ToList();
            _entries.Clear();
            return result;
        }
    }

    /// <summary>
    /// 获取条目数量
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count;
            }
        }
    }
}
