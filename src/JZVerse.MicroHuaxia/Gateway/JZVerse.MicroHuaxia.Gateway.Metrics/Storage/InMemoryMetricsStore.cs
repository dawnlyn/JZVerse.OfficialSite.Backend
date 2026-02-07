using System.Collections.Concurrent;
using JZVerse.MicroHuaxia.Gateway.Metrics.Configuration;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Gateway.Metrics.Storage;

/// <summary>
/// 内存指标存储实现
/// </summary>
public sealed class InMemoryMetricsStore : IMetricsStore, IDisposable
{
    private readonly InMemoryMetricsStoreOptions _options;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, MetricBucket>> _buckets = new();
    private readonly ConcurrentQueue<(string metricKey, string tagsKey, DateTimeOffset addedAt)> _bucketOrder =
        new();
    private readonly Timer _cleanupTimer;
    private readonly object _cleanupLock = new();
    private long _totalDataPoints;
    private bool _disposed;

    /// <summary>
    /// 创建内存指标存储
    /// </summary>
    public InMemoryMetricsStore(IOptions<InMemoryMetricsStoreOptions> options)
    {
        _options = options.Value;

        // 启动定期清理任务
        _cleanupTimer = new Timer(
            _ => Cleanup(),
            null,
            TimeSpan.FromMinutes(_options.CleanupIntervalMinutes),
            TimeSpan.FromMinutes(_options.CleanupIntervalMinutes)
        );
    }

    /// <inheritdoc />
    public void Record(
        string name,
        double value,
        IReadOnlyDictionary<string, object?>? tags = null,
        MetricType type = MetricType.Counter
    )
    {
        if (_disposed)
            return;

        tags ??= new Dictionary<string, object?>();
        var now = DateTimeOffset.UtcNow;
        var bucketStart = GetBucketStartTime(now);

        var metricBuckets = _buckets.GetOrAdd(name, _ => new ConcurrentDictionary<string, MetricBucket>());

        var tagsKey = CreateTagsKey(tags);
        var bucketKey = $"{bucketStart.Ticks}:{tagsKey}";

        var bucket = metricBuckets.GetOrAdd(
            bucketKey,
            _ =>
            {
                var newBucket = new MetricBucket(
                    name,
                    bucketStart,
                    TimeSpan.FromSeconds(_options.BucketSizeSeconds),
                    tags
                );
                _bucketOrder.Enqueue((name, bucketKey, now));
                return newBucket;
            }
        );

        bucket.AddValue(value);
        Interlocked.Increment(ref _totalDataPoints);

        // 检查是否需要清理
        if (Interlocked.Read(ref _totalDataPoints) > _options.MaxDataPoints)
        {
            CleanupOldestBuckets();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<MetricDataPoint> GetTimeSeries(
        string name,
        DateTimeOffset start,
        DateTimeOffset end,
        IReadOnlyDictionary<string, object?>? tags = null
    )
    {
        if (!_buckets.TryGetValue(name, out var metricBuckets))
        {
            return [];
        }

        var result = new List<MetricDataPoint>();
        var tagsFilter = tags != null ? CreateTagsKey(tags) : null;

        foreach (var (bucketKey, bucket) in metricBuckets)
        {
            if (bucket.StartTime > end || bucket.EndTime < start)
                continue;

            if (tagsFilter != null && bucket.TagsKey != tagsFilter)
                continue;

            var values = bucket.GetValues();
            var timestamp = bucket.StartTime;
            var increment = TimeSpan.FromSeconds(_options.BucketSizeSeconds) / Math.Max(values.Count, 1);

            foreach (var value in values)
            {
                if (timestamp >= start && timestamp <= end)
                {
                    result.Add(
                        new MetricDataPoint
                        {
                            Name = name,
                            Value = value,
                            Timestamp = timestamp,
                            Tags = bucket.Tags,
                        }
                    );
                }
                timestamp = timestamp.Add(increment);
            }
        }

        return result.OrderBy(p => p.Timestamp).ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<MetricAggregation> GetAggregations(
        string name,
        DateTimeOffset start,
        DateTimeOffset end,
        TimeSpan bucketSize,
        IReadOnlyDictionary<string, object?>? tags = null
    )
    {
        if (!_buckets.TryGetValue(name, out var metricBuckets))
        {
            return [];
        }

        var tagsFilter = tags != null ? CreateTagsKey(tags) : null;

        // 收集所有符合条件的分桶
        var matchingBuckets = metricBuckets
            .Values.Where(b =>
                b.StartTime <= end && b.EndTime >= start && (tagsFilter == null || b.TagsKey == tagsFilter)
            )
            .ToList();

        if (matchingBuckets.Count == 0)
        {
            return [];
        }

        // 按时间窗口重新聚合
        var result = new List<MetricAggregation>();
        var currentStart = start;

        while (currentStart < end)
        {
            var currentEnd = currentStart.Add(bucketSize);
            if (currentEnd > end)
                currentEnd = end;

            var windowBuckets = matchingBuckets
                .Where(b => b.StartTime < currentEnd && b.EndTime > currentStart)
                .ToList();

            if (windowBuckets.Count > 0)
            {
                var allValues = windowBuckets.SelectMany(b => b.GetValues()).OrderBy(v => v).ToList();

                if (allValues.Count > 0)
                {
                    result.Add(
                        new MetricAggregation
                        {
                            Name = name,
                            StartTime = currentStart,
                            EndTime = currentEnd,
                            Tags = windowBuckets[0].Tags,
                            Count = allValues.Count,
                            Sum = allValues.Sum(),
                            Min = allValues[0],
                            Max = allValues[^1],
                            Average = allValues.Average(),
                            P50 = GetPercentile(allValues, 50),
                            P90 = GetPercentile(allValues, 90),
                            P95 = GetPercentile(allValues, 95),
                            P99 = GetPercentile(allValues, 99),
                        }
                    );
                }
            }

            currentStart = currentEnd;
        }

        return result;
    }

    /// <inheritdoc />
    public MetricDataPoint? GetLatest(string name, IReadOnlyDictionary<string, object?>? tags = null)
    {
        if (!_buckets.TryGetValue(name, out var metricBuckets))
        {
            return null;
        }

        var tagsFilter = tags != null ? CreateTagsKey(tags) : null;

        var latestBucket = metricBuckets
            .Values.Where(b => tagsFilter == null || b.TagsKey == tagsFilter)
            .OrderByDescending(b => b.StartTime)
            .FirstOrDefault();

        if (latestBucket == null)
        {
            return null;
        }

        var values = latestBucket.GetValues();
        if (values.Count == 0)
        {
            return null;
        }

        return new MetricDataPoint
        {
            Name = name,
            Value = values[^1],
            Timestamp = latestBucket.EndTime,
            Tags = latestBucket.Tags,
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetMetricNames() => [.. _buckets.Keys];

    /// <inheritdoc />
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> GetTagCombinations(string name)
    {
        if (!_buckets.TryGetValue(name, out var metricBuckets))
        {
            return [];
        }

        return metricBuckets
            .Values.Select(b => b.Tags)
            .DistinctBy(t => CreateTagsKey(t))
            .ToList();
    }

    /// <inheritdoc />
    public void Cleanup()
    {
        if (_disposed)
            return;

        lock (_cleanupLock)
        {
            var cutoffTime = DateTimeOffset.UtcNow.AddMinutes(-_options.RetentionMinutes);
            var removedCount = 0L;

            foreach (var (name, metricBuckets) in _buckets)
            {
                var keysToRemove = metricBuckets
                    .Where(kv => kv.Value.EndTime < cutoffTime)
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    if (metricBuckets.TryRemove(key, out var bucket))
                    {
                        removedCount += bucket.Count;
                    }
                }

                // 移除空的指标
                if (metricBuckets.IsEmpty)
                {
                    _buckets.TryRemove(name, out _);
                }
            }

            Interlocked.Add(ref _totalDataPoints, -removedCount);
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_cleanupLock)
        {
            _buckets.Clear();
            while (_bucketOrder.TryDequeue(out _)) { }
            Interlocked.Exchange(ref _totalDataPoints, 0);
        }
    }

    /// <inheritdoc />
    public MetricsStoreStatistics GetStatistics()
    {
        DateTimeOffset? oldest = null;
        DateTimeOffset? newest = null;
        var bucketCount = 0;

        foreach (var metricBuckets in _buckets.Values)
        {
            foreach (var bucket in metricBuckets.Values)
            {
                bucketCount++;
                if (oldest == null || bucket.StartTime < oldest)
                    oldest = bucket.StartTime;
                if (newest == null || bucket.EndTime > newest)
                    newest = bucket.EndTime;
            }
        }

        // 估算内存使用（粗略估计）
        var estimatedMemory = Interlocked.Read(ref _totalDataPoints) * 32 + bucketCount * 256;

        return new MetricsStoreStatistics
        {
            TotalDataPoints = Interlocked.Read(ref _totalDataPoints),
            MetricCount = _buckets.Count,
            OldestDataTime = oldest,
            NewestDataTime = newest,
            BucketCount = bucketCount,
            EstimatedMemoryBytes = estimatedMemory,
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _cleanupTimer.Dispose();
    }

    private DateTimeOffset GetBucketStartTime(DateTimeOffset timestamp)
    {
        var bucketSize = TimeSpan.FromSeconds(_options.BucketSizeSeconds);
        var ticks = timestamp.UtcTicks;
        var bucketTicks = ticks - (ticks % bucketSize.Ticks);
        return new DateTimeOffset(bucketTicks, TimeSpan.Zero);
    }

    private void CleanupOldestBuckets()
    {
        lock (_cleanupLock)
        {
            var targetCount = _options.MaxDataPoints * 0.8; // 清理到 80%

            while (Interlocked.Read(ref _totalDataPoints) > targetCount && _bucketOrder.TryDequeue(out var item))
            {
                var (metricKey, bucketKey, _) = item;

                if (_buckets.TryGetValue(metricKey, out var metricBuckets))
                {
                    if (metricBuckets.TryRemove(bucketKey, out var bucket))
                    {
                        Interlocked.Add(ref _totalDataPoints, -bucket.Count);
                    }
                }
            }
        }
    }

    private static string CreateTagsKey(IReadOnlyDictionary<string, object?> tags)
    {
        if (tags.Count == 0)
            return string.Empty;

        var parts = tags.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}");
        return string.Join(",", parts);
    }

    private static double GetPercentile(List<double> sorted, int percentile)
    {
        if (sorted.Count == 0)
            return 0;
        if (sorted.Count == 1)
            return sorted[0];

        var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
    }
}
