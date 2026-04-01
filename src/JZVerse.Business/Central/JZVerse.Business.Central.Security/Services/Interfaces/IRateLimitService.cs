namespace JZVerse.Business.Central.Security.Services.Interfaces;

/// <summary>
/// 限流服务接口
/// </summary>
public interface IRateLimitService
{
    /// <summary>
    /// 检查是否允许请求（固定窗口算法）
    /// </summary>
    /// <param name="keyType">限流键类型</param>
    /// <param name="keyValue">限流键值</param>
    /// <param name="limit">限制次数</param>
    /// <param name="windowSeconds">窗口大小（秒）</param>
    /// <returns>限流检查结果</returns>
    Task<RateLimitCheckResult> CheckFixedWindowAsync(string keyType, string keyValue, int limit, int windowSeconds);

    /// <summary>
    /// 检查是否允许请求（滑动窗口算法）
    /// </summary>
    /// <param name="keyType">限流键类型</param>
    /// <param name="keyValue">限流键值</param>
    /// <param name="limit">限制次数</param>
    /// <param name="windowSeconds">窗口大小（秒）</param>
    /// <returns>限流检查结果</returns>
    Task<RateLimitCheckResult> CheckSlidingWindowAsync(string keyType, string keyValue, int limit, int windowSeconds);

    /// <summary>
    /// 检查IP限流
    /// </summary>
    /// <param name="ipAddress">IP地址</param>
    /// <param name="limit">限制次数</param>
    /// <param name="windowSeconds">窗口大小（秒）</param>
    /// <returns>限流检查结果</returns>
    Task<RateLimitCheckResult> CheckIpLimitAsync(string ipAddress, int limit, int windowSeconds);

    /// <summary>
    /// 检查用户限流
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="limit">限制次数</param>
    /// <param name="windowSeconds">窗口大小（秒）</param>
    /// <returns>限流检查结果</returns>
    Task<RateLimitCheckResult> CheckUserLimitAsync(Guid userId, int limit, int windowSeconds);

    /// <summary>
    /// 检查端点限流
    /// </summary>
    /// <param name="endpoint">端点路径</param>
    /// <param name="limit">限制次数</param>
    /// <param name="windowSeconds">窗口大小（秒）</param>
    /// <returns>限流检查结果</returns>
    Task<RateLimitCheckResult> CheckEndpointLimitAsync(string endpoint, int limit, int windowSeconds);

    /// <summary>
    /// 记录请求（用于限流计数）
    /// </summary>
    /// <param name="keyType">限流键类型</param>
    /// <param name="keyValue">限流键值</param>
    /// <param name="windowSeconds">窗口大小（秒）</param>
    /// <returns>当前计数</returns>
    Task<int> RecordRequestAsync(string keyType, string keyValue, int windowSeconds);

    /// <summary>
    /// 清理过期限流记录
    /// </summary>
    /// <returns>清理数量</returns>
    Task<int> CleanExpiredRecordsAsync();

    /// <summary>
    /// 获取限流状态
    /// </summary>
    /// <param name="keyType">限流键类型</param>
    /// <param name="keyValue">限流键值</param>
    /// <returns>限流状态</returns>
    Task<RateLimitStatus?> GetRateLimitStatusAsync(string keyType, string keyValue);

    /// <summary>
    /// 健康检查
    /// </summary>
    Task<bool> HealthCheckAsync();
}

/// <summary>
/// 限流检查结果
/// </summary>
public class RateLimitCheckResult
{
    /// <summary>
    /// 是否允许请求
    /// </summary>
    public bool IsAllowed { get; set; }

    /// <summary>
    /// 剩余请求次数
    /// </summary>
    public int Remaining { get; set; }

    /// <summary>
    /// 限流上限
    /// </summary>
    public int Limit { get; set; }

    /// <summary>
    /// 窗口重置时间
    /// </summary>
    public DateTime ResetTime { get; set; }

    /// <summary>
    /// 当前请求计数
    /// </summary>
    public int CurrentCount { get; set; }

    /// <summary>
    /// 是否已被限流
    /// </summary>
    public bool IsLimited { get; set; }

    public static RateLimitCheckResult Allowed(int remaining, int limit, DateTime resetTime, int currentCount)
    {
        return new RateLimitCheckResult
        {
            IsAllowed = true,
            Remaining = remaining,
            Limit = limit,
            ResetTime = resetTime,
            CurrentCount = currentCount,
            IsLimited = false
        };
    }

    public static RateLimitCheckResult Denied(int limit, DateTime resetTime, int currentCount)
    {
        return new RateLimitCheckResult
        {
            IsAllowed = false,
            Remaining = 0,
            Limit = limit,
            ResetTime = resetTime,
            CurrentCount = currentCount,
            IsLimited = true
        };
    }
}

/// <summary>
/// 限流状态
/// </summary>
public class RateLimitStatus
{
    /// <summary>
    /// 限流键类型
    /// </summary>
    public string KeyType { get; set; } = string.Empty;

    /// <summary>
    /// 限流键值
    /// </summary>
    public string KeyValue { get; set; } = string.Empty;

    /// <summary>
    /// 请求计数
    /// </summary>
    public int RequestCount { get; set; }

    /// <summary>
    /// 窗口开始时间
    /// </summary>
    public DateTime WindowStart { get; set; }

    /// <summary>
    /// 窗口结束时间
    /// </summary>
    public DateTime WindowEnd { get; set; }

    /// <summary>
    /// 是否已被限流
    /// </summary>
    public bool IsLimited { get; set; }

    /// <summary>
    /// 限流触发次数
    /// </summary>
    public int LimitTriggeredCount { get; set; }
}
