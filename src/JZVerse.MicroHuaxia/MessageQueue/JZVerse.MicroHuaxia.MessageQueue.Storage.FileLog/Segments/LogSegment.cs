using System.Buffers.Binary;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.MessageQueue.Storage.FileLog.Segments;

/// <summary>
/// 日志段管理器
/// </summary>
public sealed class LogSegment : IDisposable
{
    private readonly ILogger _logger;
    private readonly string _basePath;
    private readonly long _baseOffset;
    private readonly FileLogStoreOptions _options;

    private FileStream? _logStream;
    private FileStream? _indexStream;
    private FileStream? _timeIndexStream;

    private long _currentPosition;
    private long _nextOffset;
    private int _indexEntryCount;

    private readonly Lock _writeLock = new();
    private bool _disposed;

    /// <summary>
    /// 段基础偏移量
    /// </summary>
    public long BaseOffset => _baseOffset;

    /// <summary>
    /// 下一个偏移量
    /// </summary>
    public long NextOffset => _nextOffset;

    /// <summary>
    /// 日志文件大小
    /// </summary>
    public long Size => _currentPosition;

    /// <summary>
    /// 是否已满
    /// </summary>
    public bool IsFull => _currentPosition >= _options.SegmentSizeBytes;

    public LogSegment(
        string basePath,
        long baseOffset,
        FileLogStoreOptions options,
        ILogger logger)
    {
        _basePath = basePath;
        _baseOffset = baseOffset;
        _options = options;
        _logger = logger;
        _nextOffset = baseOffset;

        EnsureDirectoryExists();
        OpenFiles();
    }

    /// <summary>
    /// 追加记录
    /// </summary>
    public long Append(MessageRecord record)
    {
        lock (_writeLock)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(LogSegment));
            }

            var offset = _nextOffset;
            var position = _currentPosition;

            // 写入日志
            var bytes = record.ToBytes();
            _logStream!.Write(bytes);

            // 更新索引（稀疏索引）
            if (_currentPosition - GetLastIndexedPosition() >= _options.IndexIntervalBytes)
            {
                WriteIndexEntry(offset, position);
            }

            // 更新时间索引
            WriteTimeIndexEntry(record.Timestamp, offset);

            _currentPosition += bytes.Length;
            _nextOffset++;

            // 根据刷盘策略决定是否立即刷盘
            if (_options.FlushPolicy == FlushPolicy.EveryMessage)
            {
                Flush();
            }

            return offset;
        }
    }

    /// <summary>
    /// 按偏移量读取记录
    /// </summary>
    public MessageRecord? Read(long offset)
    {
        if (offset < _baseOffset || offset >= _nextOffset)
        {
            return null;
        }

        var position = FindPositionByOffset(offset);
        if (position < 0)
        {
            return null;
        }

        return ReadRecordAtPosition(position);
    }

    /// <summary>
    /// 按偏移量范围读取记录
    /// </summary>
    public IReadOnlyList<MessageRecord> ReadRange(long startOffset, int count)
    {
        var records = new List<MessageRecord>(count);

        var position = FindPositionByOffset(startOffset);
        if (position < 0)
        {
            return records;
        }

        using var reader = new BinaryReader(new FileStream(GetLogFilePath(), FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        reader.BaseStream.Seek(position, SeekOrigin.Begin);

        var currentOffset = startOffset;
        while (records.Count < count && currentOffset < _nextOffset && reader.BaseStream.Position < _currentPosition)
        {
            try
            {
                var headerBytes = reader.ReadBytes(MessageRecord.HeaderLength);
                if (headerBytes.Length < MessageRecord.HeaderLength)
                {
                    break;
                }

                var length = BinaryPrimitives.ReadInt32BigEndian(headerBytes);
                reader.BaseStream.Seek(-MessageRecord.HeaderLength, SeekOrigin.Current);

                var recordBytes = reader.ReadBytes(length);
                var record = MessageRecord.FromBytes(recordBytes);
                records.Add(record);

                currentOffset++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading record at offset {Offset}", currentOffset);
                break;
            }
        }

        return records;
    }

    /// <summary>
    /// 按时间范围查找偏移量
    /// </summary>
    public long? FindOffsetByTimestamp(long timestamp)
    {
        // 从时间索引中查找
        var timeIndexPath = GetTimeIndexFilePath();
        if (!File.Exists(timeIndexPath))
        {
            return null;
        }

        using var reader = new BinaryReader(new FileStream(timeIndexPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        long? result = null;

        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            var entryTimestamp = reader.ReadInt64();
            var entryOffset = reader.ReadInt64();

            if (entryTimestamp <= timestamp)
            {
                result = entryOffset;
            }
            else
            {
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// 刷盘
    /// </summary>
    public void Flush()
    {
        _logStream?.Flush();
        _indexStream?.Flush();
        _timeIndexStream?.Flush();
    }

    /// <summary>
    /// 关闭段
    /// </summary>
    public void Close()
    {
        Flush();
        _logStream?.Close();
        _indexStream?.Close();
        _timeIndexStream?.Close();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Close();
        _logStream?.Dispose();
        _indexStream?.Dispose();
        _timeIndexStream?.Dispose();
    }

    private void EnsureDirectoryExists()
    {
        var dir = Path.GetDirectoryName(_basePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    private void OpenFiles()
    {
        _logStream = new FileStream(
            GetLogFilePath(),
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.Read);

        _indexStream = new FileStream(
            GetIndexFilePath(),
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.Read);

        _timeIndexStream = new FileStream(
            GetTimeIndexFilePath(),
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.Read);

        // 定位到文件末尾
        _currentPosition = _logStream.Length;
        _logStream.Seek(0, SeekOrigin.End);
        _indexStream.Seek(0, SeekOrigin.End);
        _timeIndexStream.Seek(0, SeekOrigin.End);

        // 计算已有的消息数量
        _indexEntryCount = (int)(_indexStream.Length / 16); // 每个索引条目 16 字节
        _nextOffset = _baseOffset + CountMessages();
    }

    private int CountMessages()
    {
        // 通过遍历索引文件或日志文件计算消息数量
        // 简化实现：假设索引间隔固定，估算消息数量
        if (_currentPosition == 0)
        {
            return 0;
        }

        // 读取最后一个索引条目
        if (_indexEntryCount > 0)
        {
            _indexStream!.Seek(-16, SeekOrigin.End);
            var buffer = new byte[16];
            _indexStream.ReadExactly(buffer);

            var lastOffset = BinaryPrimitives.ReadInt64BigEndian(buffer.AsSpan()[..8]);
            return (int)(lastOffset - _baseOffset + 1);
        }

        return 0;
    }

    private long FindPositionByOffset(long offset)
    {
        // 从索引中查找最接近的位置
        var relativeOffset = offset - _baseOffset;

        // 二分查找索引
        var indexPath = GetIndexFilePath();
        if (!File.Exists(indexPath) || _indexEntryCount == 0)
        {
            return 0; // 从头开始扫描
        }

        using var reader = new BinaryReader(new FileStream(indexPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        long position = 0;

        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            var entryOffset = reader.ReadInt64();
            var entryPosition = reader.ReadInt64();

            if (entryOffset <= offset)
            {
                position = entryPosition;
            }
            else
            {
                break;
            }
        }

        return position;
    }

    private MessageRecord? ReadRecordAtPosition(long position)
    {
        using var reader = new BinaryReader(new FileStream(GetLogFilePath(), FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        reader.BaseStream.Seek(position, SeekOrigin.Begin);

        var headerBytes = reader.ReadBytes(MessageRecord.HeaderLength);
        if (headerBytes.Length < MessageRecord.HeaderLength)
        {
            return null;
        }

        var length = BinaryPrimitives.ReadInt32BigEndian(headerBytes);
        reader.BaseStream.Seek(position, SeekOrigin.Begin);

        var recordBytes = reader.ReadBytes(length);
        return MessageRecord.FromBytes(recordBytes);
    }

    private void WriteIndexEntry(long offset, long position)
    {
        var buffer = new byte[16];
        BinaryPrimitives.WriteInt64BigEndian(buffer.AsSpan()[..8], offset);
        BinaryPrimitives.WriteInt64BigEndian(buffer.AsSpan()[8..], position);
        _indexStream!.Write(buffer);
        _indexEntryCount++;
    }

    private void WriteTimeIndexEntry(long timestamp, long offset)
    {
        var buffer = new byte[16];
        BinaryPrimitives.WriteInt64BigEndian(buffer.AsSpan()[..8], timestamp);
        BinaryPrimitives.WriteInt64BigEndian(buffer.AsSpan()[8..], offset);
        _timeIndexStream!.Write(buffer);
    }

    private long GetLastIndexedPosition()
    {
        if (_indexEntryCount == 0)
        {
            return 0;
        }

        _indexStream!.Seek(-8, SeekOrigin.End);
        var buffer = new byte[8];
        _indexStream.ReadExactly(buffer);
        _indexStream.Seek(0, SeekOrigin.End);

        return BinaryPrimitives.ReadInt64BigEndian(buffer);
    }

    private string GetLogFilePath() => $"{_basePath}.log";
    private string GetIndexFilePath() => $"{_basePath}.index";
    private string GetTimeIndexFilePath() => $"{_basePath}.timeindex";
}
