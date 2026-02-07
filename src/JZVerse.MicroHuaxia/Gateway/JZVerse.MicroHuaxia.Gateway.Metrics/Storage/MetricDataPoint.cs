namespace JZVerse.MicroHuaxia.Gateway.Metrics.Storage;

/// <summary>
/// 指标数据点
/// </summary>
public sealed record MetricDataPoint
{
    /// <summary>
    /// 指标名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 指标值
    /// </summary>
    public required double Value { get; init; }

    /// <summary>
    /// 时间戳
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// 标签
    /// </summary>
    public IReadOnlyDictionary<string, object?> Tags { get; init; } = new Dictionary<string, object?>();

    /// <summary>
    /// 指标类型
    /// </summary>
    public MetricType Type { get; init; } = MetricType.Counter;
}

/// <summary>
/// 指标类型
/// </summary>
public enum MetricType
{
    /// <summary>
    /// 计数器
    /// </summary>
    Counter,

    /// <summary>
    /// 直方图
    /// </summary>
    Histogram,

    /// <summary>
    /// 计量器
    /// </summary>
    Gauge,
}

/// <summary>
/// 聚合类型
/// </summary>
public enum AggregationType
{
    /// <summary>
    /// 求和
    /// </summary>
    Sum,

    /// <summary>
    /// 平均值
    /// </summary>
    Average,

    /// <summary>
    /// 最小值
    /// </summary>
    Min,

    /// <summary>
    /// 最大值
    /// </summary>
    Max,

    /// <summary>
    /// 计数
    /// </summary>
    Count,

    /// <summary>
    /// 速率（每秒）
    /// </summary>
    Rate,

    /// <summary>
    /// P50 百分位
    /// </summary>
    P50,

    /// <summary>
    /// P90 百分位
    /// </summary>
    P90,

    /// <summary>
    /// P95 百分位
    /// </summary>
    P95,

    /// <summary>
    /// P99 百分位
    /// </summary>
    P99,
}
