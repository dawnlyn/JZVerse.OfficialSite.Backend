using JZVerse.DataAccess.Abstractions;
using JZVerse.DataAccess.Abstractions.Models;
using Microsoft.Extensions.Options;

namespace JZVerse.DataAccess.Core.IdGeneration;

/// <summary>
/// 128 位雪花 ID 生成器实现
/// </summary>
/// <remarks>
/// 线程安全，支持时钟回拨处理
/// </remarks>
public sealed class SnowflakeIdGenerator : ISnowflakeIdGenerator
{
    private readonly int _datacenterId;
    private readonly int _workerId;
    private readonly int _clockBackwardToleranceMs;

    private long _lastTimestamp = -1L;
    private int _sequence;
    private readonly object _lock = new();

    /// <summary>
    /// 创建雪花 ID 生成器
    /// </summary>
    public SnowflakeIdGenerator(IOptions<SnowflakeIdGeneratorOptions> options)
    {
        var opt = options.Value;

        if (opt.DatacenterId is < 0 or > 31)
            throw new ArgumentOutOfRangeException(nameof(opt.DatacenterId), "DatacenterId must be between 0 and 31");
        if (opt.WorkerId is < 0 or > 31)
            throw new ArgumentOutOfRangeException(nameof(opt.WorkerId), "WorkerId must be between 0 and 31");

        _datacenterId = opt.DatacenterId;
        _workerId = opt.WorkerId;
        _clockBackwardToleranceMs = opt.ClockBackwardToleranceMs;
    }

    /// <summary>
    /// 创建雪花 ID 生成器（直接指定参数）
    /// </summary>
    public SnowflakeIdGenerator(int datacenterId, int workerId, int clockBackwardToleranceMs = 5)
    {
        if (datacenterId is < 0 or > 31)
            throw new ArgumentOutOfRangeException(nameof(datacenterId), "DatacenterId must be between 0 and 31");
        if (workerId is < 0 or > 31)
            throw new ArgumentOutOfRangeException(nameof(workerId), "WorkerId must be between 0 and 31");

        _datacenterId = datacenterId;
        _workerId = workerId;
        _clockBackwardToleranceMs = clockBackwardToleranceMs;
    }

    /// <inheritdoc />
    public int DatacenterId => _datacenterId;

    /// <inheritdoc />
    public int WorkerId => _workerId;

    /// <inheritdoc />
    public SnowflakeId Generate()
    {
        lock (_lock)
        {
            var timestamp = GetCurrentTimestamp();

            // 处理时钟回拨
            if (timestamp < _lastTimestamp)
            {
                var offset = _lastTimestamp - timestamp;
                if (offset <= _clockBackwardToleranceMs)
                {
                    // 在容忍范围内，等待时钟追上
                    Thread.Sleep((int)offset + 1);
                    timestamp = GetCurrentTimestamp();
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Clock moved backwards. Refusing to generate id for {offset}ms");
                }
            }

            // 同一毫秒内，序列号递增
            if (timestamp == _lastTimestamp)
            {
                _sequence = (_sequence + 1) & 0x7FF; // 11 bits, max 2047
                if (_sequence == 0)
                {
                    // 序列号溢出，等待下一毫秒
                    timestamp = WaitNextMillis(_lastTimestamp);
                }
            }
            else
            {
                _sequence = 0;
            }

            _lastTimestamp = timestamp;

            return new SnowflakeId(timestamp, _datacenterId, _workerId, _sequence);
        }
    }

    /// <inheritdoc />
    public Guid GenerateGuid()
    {
        return Generate().ToGuid();
    }

    /// <inheritdoc />
    public IReadOnlyList<SnowflakeId> GenerateBatch(int count)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be positive");

        var result = new SnowflakeId[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = Generate();
        }
        return result;
    }

    /// <inheritdoc />
    public IReadOnlyList<Guid> GenerateGuidBatch(int count)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be positive");

        var result = new Guid[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = GenerateGuid();
        }
        return result;
    }

    /// <inheritdoc />
    public SnowflakeId FromGuid(Guid guid)
    {
        return SnowflakeId.FromGuid(guid);
    }

    private static long GetCurrentTimestamp()
    {
        return (long)(DateTimeOffset.UtcNow - SnowflakeId.Epoch).TotalMilliseconds;
    }

    private static long WaitNextMillis(long lastTimestamp)
    {
        var timestamp = GetCurrentTimestamp();
        while (timestamp <= lastTimestamp)
        {
            Thread.SpinWait(100);
            timestamp = GetCurrentTimestamp();
        }
        return timestamp;
    }
}
