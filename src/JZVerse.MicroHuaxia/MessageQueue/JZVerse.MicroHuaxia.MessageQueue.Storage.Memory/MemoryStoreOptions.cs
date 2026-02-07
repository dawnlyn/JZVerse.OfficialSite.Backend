namespace JZVerse.MicroHuaxia.MessageQueue.Storage.Memory;

/// <summary>
/// 内存存储选项
/// </summary>
public sealed class MemoryStoreOptions
{
    /// <summary>
    /// 每个分区的最大消息数
    /// </summary>
    public int MaxMessagesPerPartition { get; set; } = 100_000;

    /// <summary>
    /// 消息过期时间
    /// </summary>
    public TimeSpan? MessageTtl { get; set; }

    /// <summary>
    /// 是否启用 LRU 缓存
    /// </summary>
    public bool EnableLruCache { get; set; } = true;

    /// <summary>
    /// LRU 缓存大小
    /// </summary>
    public int LruCacheSize { get; set; } = 10_000;
}
