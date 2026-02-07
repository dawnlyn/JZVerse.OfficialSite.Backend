using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Storage;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.MessageQueue.Storage.FileLog;

/// <summary>
/// 基于文件的偏移量管理器
/// 将消费偏移量持久化到文件系统
/// </summary>
public sealed class FileLogOffsetManager : IOffsetManager, IDisposable
{
    private readonly ILogger<FileLogOffsetManager> _logger;
    private readonly FileLogStoreOptions _options;
    private readonly IMessageStore _messageStore;

    // 内存缓存: (consumerGroup, topic, partition) -> offset
    private readonly ConcurrentDictionary<(string ConsumerGroup, string Topic, int Partition), long> _offsetCache = new();

    private readonly string _offsetDirectory;
    private readonly Timer _flushTimer;
    private bool _dirty;
    private bool _disposed;

    public FileLogOffsetManager(
        ILogger<FileLogOffsetManager> logger, 
        FileLogStoreOptions options,
        IMessageStore messageStore)
    {
        _logger = logger;
        _options = options;
        _messageStore = messageStore;
        _offsetDirectory = Path.Combine(options.DataDirectory, "__offsets");

        EnsureDirectoryExists();
        LoadOffsets();

        // 定时刷盘
        _flushTimer = new Timer(FlushOffsets, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    /// <inheritdoc />
    public Task<long> GetOffsetAsync(
        string consumerGroup, 
        string topic, 
        int partition, 
        CancellationToken cancellationToken = default)
    {
        var offset = _offsetCache.GetValueOrDefault((consumerGroup, topic, partition), -1);
        return Task.FromResult(offset);
    }

    /// <inheritdoc />
    public Task CommitOffsetAsync(
        string consumerGroup, 
        string topic, 
        int partition, 
        long offset, 
        CancellationToken cancellationToken = default)
    {
        _offsetCache[(consumerGroup, topic, partition)] = offset;
        _dirty = true;

        _logger.LogDebug("Offset committed: [{ConsumerGroup}] {Topic}:{Partition} -> {Offset}",
            consumerGroup, topic, partition, offset);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ResetOffsetAsync(
        string consumerGroup, 
        string topic, 
        int partition, 
        long offset, 
        CancellationToken cancellationToken = default)
    {
        _offsetCache[(consumerGroup, topic, partition)] = offset;
        _dirty = true;

        _logger.LogInformation("Offset reset: [{ConsumerGroup}] {Topic}:{Partition} -> {Offset}",
            consumerGroup, topic, partition, offset);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<long> GetLagAsync(
        string consumerGroup, 
        string topic, 
        int partition, 
        CancellationToken cancellationToken = default)
    {
        var currentOffset = await GetOffsetAsync(consumerGroup, topic, partition, cancellationToken);
        var latestOffset = await _messageStore.GetLatestOffsetAsync(topic, partition, cancellationToken);

        if (currentOffset < 0)
        {
            return latestOffset;
        }

        return Math.Max(0, latestOffset - currentOffset);
    }

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_offsetDirectory))
        {
            Directory.CreateDirectory(_offsetDirectory);
        }
    }

    private void LoadOffsets()
    {
        if (!Directory.Exists(_offsetDirectory))
        {
            return;
        }

        var offsetFiles = Directory.GetFiles(_offsetDirectory, "*.offset");
        foreach (var file in offsetFiles)
        {
            try
            {
                LoadOffsetFile(file);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load offset file: {File}", file);
            }
        }

        _logger.LogInformation("Loaded {Count} consumer group offsets", _offsetCache.Count);
    }

    private void LoadOffsetFile(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(fs);

        var consumerGroup = Path.GetFileNameWithoutExtension(filePath);

        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            // 格式: [TopicLength:2][Topic:*][Partition:4][Offset:8]
            var topicLength = reader.ReadInt16();
            var topicBytes = reader.ReadBytes(topicLength);
            var topic = System.Text.Encoding.UTF8.GetString(topicBytes);
            var partition = reader.ReadInt32();
            var offset = reader.ReadInt64();

            _offsetCache[(consumerGroup, topic, partition)] = offset;
        }
    }

    private void FlushOffsets(object? state)
    {
        if (!_dirty) return;

        try
        {
            // 按消费组分组
            var groupedOffsets = _offsetCache
                .GroupBy(kv => kv.Key.ConsumerGroup)
                .ToList();

            foreach (var group in groupedOffsets)
            {
                var consumerGroup = group.Key;
                var filePath = Path.Combine(_offsetDirectory, $"{SanitizeFileName(consumerGroup)}.offset");
                var tempPath = $"{filePath}.tmp";

                using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                using (var writer = new BinaryWriter(fs))
                {
                    foreach (var kv in group)
                    {
                        var topicBytes = System.Text.Encoding.UTF8.GetBytes(kv.Key.Topic);
                        writer.Write((short)topicBytes.Length);
                        writer.Write(topicBytes);
                        writer.Write(kv.Key.Partition);
                        writer.Write(kv.Value);
                    }
                }

                // 原子替换
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                File.Move(tempPath, filePath);
            }

            _dirty = false;
            _logger.LogDebug("Offsets flushed to disk");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush offsets to disk");
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _flushTimer.Dispose();

        // 最终刷盘
        if (_dirty)
        {
            FlushOffsets(null);
        }
    }
}
