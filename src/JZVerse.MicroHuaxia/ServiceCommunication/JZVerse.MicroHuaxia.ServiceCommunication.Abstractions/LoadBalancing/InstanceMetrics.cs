namespace JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.LoadBalancing;

/// <summary>
/// 服务实例指标收集器接口
/// </summary>
public interface IInstanceMetricsCollector
{
    /// <summary>
    /// 记录请求结果
    /// </summary>
    /// <param name="instanceId">实例 ID</param>
    /// <param name="success">是否成功</param>
    /// <param name="duration">响应时间</param>
    void RecordRequest(string instanceId, bool success, TimeSpan duration);

    /// <summary>
    /// 增加活跃连接数
    /// </summary>
    void IncrementActiveConnections(string instanceId);

    /// <summary>
    /// 减少活跃连接数
    /// </summary>
    void DecrementActiveConnections(string instanceId);

    /// <summary>
    /// 获取实例指标
    /// </summary>
    InstanceMetrics? GetMetrics(string instanceId);

    /// <summary>
    /// 获取所有实例指标
    /// </summary>
    IReadOnlyDictionary<string, InstanceMetrics> GetAllMetrics();

    /// <summary>
    /// 清除指定实例的指标
    /// </summary>
    void ClearMetrics(string instanceId);
}

/// <summary>
/// 服务实例运行时指标
/// </summary>
public sealed class InstanceMetrics
{
    private readonly object _lock = new();
    private readonly Queue<TimeSpan> _responseTimes = new();
    private const int MaxSamples = 100;

    /// <summary>
    /// 实例 ID
    /// </summary>
    public string InstanceId { get; }

    /// <summary>
    /// 成功请求数
    /// </summary>
    public long SuccessCount { get; private set; }

    /// <summary>
    /// 失败请求数
    /// </summary>
    public long FailureCount { get; private set; }

    /// <summary>
    /// 活跃连接数
    /// </summary>
    public int ActiveConnections { get; private set; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTimeOffset LastUpdateTime { get; private set; }

    /// <summary>
    /// 计算的权重（0-100）
    /// </summary>
    public double CalculatedWeight { get; private set; } = 50;

    /// <summary>
    /// 成功率
    /// </summary>
    public double SuccessRate
    {
        get
        {
            var total = SuccessCount + FailureCount;
            return total > 0 ? (double)SuccessCount / total : 1.0;
        }
    }

    /// <summary>
    /// 平均响应时间
    /// </summary>
    public TimeSpan AverageResponseTime
    {
        get
        {
            lock (_lock)
            {
                if (_responseTimes.Count == 0)
                {
                    return TimeSpan.Zero;
                }

                var totalMs = _responseTimes.Sum(t => t.TotalMilliseconds);
                return TimeSpan.FromMilliseconds(totalMs / _responseTimes.Count);
            }
        }
    }

    /// <summary>
    /// P95 响应时间
    /// </summary>
    public TimeSpan P95ResponseTime
    {
        get
        {
            lock (_lock)
            {
                if (_responseTimes.Count == 0)
                {
                    return TimeSpan.Zero;
                }

                var sorted = _responseTimes.OrderBy(t => t).ToList();
                var index = (int)(sorted.Count * 0.95);
                return sorted[Math.Min(index, sorted.Count - 1)];
            }
        }
    }

    public InstanceMetrics(string instanceId)
    {
        InstanceId = instanceId;
        LastUpdateTime = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// 记录请求结果
    /// </summary>
    public void RecordRequest(bool success, TimeSpan duration)
    {
        lock (_lock)
        {
            if (success)
            {
                SuccessCount++;
            }
            else
            {
                FailureCount++;
            }

            _responseTimes.Enqueue(duration);
            while (_responseTimes.Count > MaxSamples)
            {
                _responseTimes.Dequeue();
            }

            LastUpdateTime = DateTimeOffset.UtcNow;
            UpdateCalculatedWeight();
        }
    }

    /// <summary>
    /// 增加活跃连接
    /// </summary>
    public void IncrementConnections()
    {
        Interlocked.Increment(ref _activeConnections);
    }

    /// <summary>
    /// 减少活跃连接
    /// </summary>
    public void DecrementConnections()
    {
        Interlocked.Decrement(ref _activeConnections);
    }

    private int _activeConnections;

    /// <summary>
    /// 更新计算权重
    /// 权重公式: W = α * (1/AvgRT_normalized) + β * SuccessRate
    /// </summary>
    private void UpdateCalculatedWeight()
    {
        const double alpha = 0.5;  // 响应时间权重
        const double beta = 0.5;   // 成功率权重
        const double maxResponseTimeMs = 5000; // 用于归一化的最大响应时间

        var avgRt = AverageResponseTime.TotalMilliseconds;
        var rtScore = avgRt > 0
            ? Math.Max(0, 1 - (avgRt / maxResponseTimeMs))
            : 1.0;

        var successRateScore = SuccessRate;

        // 计算新权重
        var newWeight = (alpha * rtScore + beta * successRateScore) * 100;

        // 限制权重变化幅度（每次最多 ±20%）
        var maxChange = CalculatedWeight * 0.2;
        var weightChange = newWeight - CalculatedWeight;
        if (Math.Abs(weightChange) > maxChange)
        {
            weightChange = Math.Sign(weightChange) * maxChange;
        }

        CalculatedWeight = Math.Max(1, Math.Min(100, CalculatedWeight + weightChange));
        ActiveConnections = _activeConnections;
    }
}
