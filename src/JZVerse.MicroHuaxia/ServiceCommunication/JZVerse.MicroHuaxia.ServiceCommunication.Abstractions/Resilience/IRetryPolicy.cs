namespace JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.Resilience;

/// <summary>
/// 重试策略接口
/// </summary>
public interface IRetryPolicy
{
    /// <summary>
    /// 策略名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 执行带重试的操作
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="action">操作</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行带重试的操作（无返回值）
    /// </summary>
    Task ExecuteAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 重试策略配置
/// </summary>
public sealed record RetryPolicyOptions
{
    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// 初始重试延迟
    /// </summary>
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// 最大重试延迟
    /// </summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 延迟增长因子（指数退避）
    /// </summary>
    public double BackoffMultiplier { get; init; } = 2.0;

    /// <summary>
    /// 是否添加抖动
    /// </summary>
    public bool UseJitter { get; init; } = true;

    /// <summary>
    /// 可重试的异常类型
    /// </summary>
    public HashSet<Type> RetryableExceptions { get; init; } = [
        typeof(HttpRequestException),
        typeof(TimeoutException),
        typeof(OperationCanceledException)
    ];

    /// <summary>
    /// 可重试的 HTTP 状态码
    /// </summary>
    public HashSet<int> RetryableStatusCodes { get; init; } = [408, 429, 500, 502, 503, 504];
}
