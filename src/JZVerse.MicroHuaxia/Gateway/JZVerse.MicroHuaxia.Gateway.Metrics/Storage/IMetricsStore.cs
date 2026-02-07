namespace JZVerse.MicroHuaxia.Gateway.Metrics.Storage;

/// <summary>
/// 指标存储接口
/// </summary>
public interface IMetricsStore
{
    /// <summary>
    /// 记录指标数据点
    /// </summary>
    /// <param name="name">指标名称</param>
    /// <param name="value">值</param>
    /// <param name="tags">标签</param>
    /// <param name="type">指标类型</param>
    void Record(
        string name,
        double value,
        IReadOnlyDictionary<string, object?>? tags = null,
        MetricType type = MetricType.Counter
    );

    /// <summary>
    /// 获取时间序列数据
    /// </summary>
    /// <param name="name">指标名称</param>
    /// <param name="start">开始时间</param>
    /// <param name="end">结束时间</param>
    /// <param name="tags">标签过滤</param>
    /// <returns>数据点列表</returns>
    IReadOnlyList<MetricDataPoint> GetTimeSeries(
        string name,
        DateTimeOffset start,
        DateTimeOffset end,
        IReadOnlyDictionary<string, object?>? tags = null
    );

    /// <summary>
    /// 获取聚合数据
    /// </summary>
    /// <param name="name">指标名称</param>
    /// <param name="start">开始时间</param>
    /// <param name="end">结束时间</param>
    /// <param name="bucketSize">分桶大小</param>
    /// <param name="tags">标签过滤</param>
    /// <returns>聚合结果列表</returns>
    IReadOnlyList<MetricAggregation> GetAggregations(
        string name,
        DateTimeOffset start,
        DateTimeOffset end,
        TimeSpan bucketSize,
        IReadOnlyDictionary<string, object?>? tags = null
    );

    /// <summary>
    /// 获取最新的指标值
    /// </summary>
    /// <param name="name">指标名称</param>
    /// <param name="tags">标签过滤</param>
    /// <returns>最新的数据点</returns>
    MetricDataPoint? GetLatest(string name, IReadOnlyDictionary<string, object?>? tags = null);

    /// <summary>
    /// 获取所有指标名称
    /// </summary>
    /// <returns>指标名称列表</returns>
    IReadOnlyList<string> GetMetricNames();

    /// <summary>
    /// 获取指定指标的所有标签组合
    /// </summary>
    /// <param name="name">指标名称</param>
    /// <returns>标签组合列表</returns>
    IReadOnlyList<IReadOnlyDictionary<string, object?>> GetTagCombinations(string name);

    /// <summary>
    /// 清理过期数据
    /// </summary>
    void Cleanup();

    /// <summary>
    /// 清空所有数据
    /// </summary>
    void Clear();

    /// <summary>
    /// 获取存储统计信息
    /// </summary>
    /// <returns>统计信息</returns>
    MetricsStoreStatistics GetStatistics();
}

/// <summary>
/// 指标存储统计信息
/// </summary>
public sealed record MetricsStoreStatistics
{
    /// <summary>
    /// 总数据点数量
    /// </summary>
    public required long TotalDataPoints { get; init; }

    /// <summary>
    /// 指标数量
    /// </summary>
    public required int MetricCount { get; init; }

    /// <summary>
    /// 最早数据时间
    /// </summary>
    public required DateTimeOffset? OldestDataTime { get; init; }

    /// <summary>
    /// 最新数据时间
    /// </summary>
    public required DateTimeOffset? NewestDataTime { get; init; }

    /// <summary>
    /// 分桶数量
    /// </summary>
    public required int BucketCount { get; init; }

    /// <summary>
    /// 估计内存使用（字节）
    /// </summary>
    public required long EstimatedMemoryBytes { get; init; }
}
