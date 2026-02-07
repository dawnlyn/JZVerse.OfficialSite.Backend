namespace JZVerse.MicroHuaxia.Gateway.Metrics.Storage;

/// <summary>
/// 指标分桶，用于聚合同一时间窗口内的数据
/// </summary>
public sealed class MetricBucket
{
    private readonly object _lock = new();
    private readonly List<double> _values = [];

    /// <summary>
    /// 分桶开始时间
    /// </summary>
    public DateTimeOffset StartTime { get; }

    /// <summary>
    /// 分桶结束时间
    /// </summary>
    public DateTimeOffset EndTime { get; }

    /// <summary>
    /// 指标名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 标签键（用于区分不同维度的数据）
    /// </summary>
    public string TagsKey { get; }

    /// <summary>
    /// 标签
    /// </summary>
    public IReadOnlyDictionary<string, object?> Tags { get; }

    /// <summary>
    /// 数据点数量
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _values.Count;
            }
        }
    }

    /// <summary>
    /// 创建指标分桶
    /// </summary>
    public MetricBucket(
        string name,
        DateTimeOffset startTime,
        TimeSpan bucketSize,
        IReadOnlyDictionary<string, object?> tags
    )
    {
        Name = name;
        StartTime = startTime;
        EndTime = startTime.Add(bucketSize);
        Tags = tags;
        TagsKey = CreateTagsKey(tags);
    }

    /// <summary>
    /// 添加值
    /// </summary>
    public void AddValue(double value)
    {
        lock (_lock)
        {
            _values.Add(value);
        }
    }

    /// <summary>
    /// 获取聚合结果
    /// </summary>
    public MetricAggregation GetAggregation()
    {
        lock (_lock)
        {
            if (_values.Count == 0)
            {
                return new MetricAggregation
                {
                    Name = Name,
                    StartTime = StartTime,
                    EndTime = EndTime,
                    Tags = Tags,
                    Count = 0,
                    Sum = 0,
                    Min = 0,
                    Max = 0,
                    Average = 0,
                    P50 = 0,
                    P90 = 0,
                    P95 = 0,
                    P99 = 0,
                };
            }

            var sorted = _values.OrderBy(v => v).ToList();
            var count = sorted.Count;

            return new MetricAggregation
            {
                Name = Name,
                StartTime = StartTime,
                EndTime = EndTime,
                Tags = Tags,
                Count = count,
                Sum = sorted.Sum(),
                Min = sorted[0],
                Max = sorted[^1],
                Average = sorted.Average(),
                P50 = GetPercentile(sorted, 50),
                P90 = GetPercentile(sorted, 90),
                P95 = GetPercentile(sorted, 95),
                P99 = GetPercentile(sorted, 99),
            };
        }
    }

    /// <summary>
    /// 获取所有值的副本
    /// </summary>
    public IReadOnlyList<double> GetValues()
    {
        lock (_lock)
        {
            return [.. _values];
        }
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

    private static string CreateTagsKey(IReadOnlyDictionary<string, object?> tags)
    {
        if (tags.Count == 0)
            return string.Empty;

        var parts = tags.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}");
        return string.Join(",", parts);
    }
}

/// <summary>
/// 指标聚合结果
/// </summary>
public sealed record MetricAggregation
{
    /// <summary>
    /// 指标名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public required DateTimeOffset StartTime { get; init; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public required DateTimeOffset EndTime { get; init; }

    /// <summary>
    /// 标签
    /// </summary>
    public required IReadOnlyDictionary<string, object?> Tags { get; init; }

    /// <summary>
    /// 数据点数量
    /// </summary>
    public required int Count { get; init; }

    /// <summary>
    /// 总和
    /// </summary>
    public required double Sum { get; init; }

    /// <summary>
    /// 最小值
    /// </summary>
    public required double Min { get; init; }

    /// <summary>
    /// 最大值
    /// </summary>
    public required double Max { get; init; }

    /// <summary>
    /// 平均值
    /// </summary>
    public required double Average { get; init; }

    /// <summary>
    /// P50 百分位
    /// </summary>
    public required double P50 { get; init; }

    /// <summary>
    /// P90 百分位
    /// </summary>
    public required double P90 { get; init; }

    /// <summary>
    /// P95 百分位
    /// </summary>
    public required double P95 { get; init; }

    /// <summary>
    /// P99 百分位
    /// </summary>
    public required double P99 { get; init; }
}
