namespace JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.Resilience;

/// <summary>
/// 熔断器接口
/// </summary>
public interface ICircuitBreaker
{
    /// <summary>
    /// 熔断器名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 当前状态
    /// </summary>
    CircuitBreakerState State { get; }

    /// <summary>
    /// 执行带熔断保护的操作
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="action">操作</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行带熔断保护的操作（无返回值）
    /// </summary>
    Task ExecuteAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 手动打开熔断器
    /// </summary>
    void Open();

    /// <summary>
    /// 手动关闭熔断器
    /// </summary>
    void Close();

    /// <summary>
    /// 重置熔断器统计
    /// </summary>
    void Reset();
}

/// <summary>
/// 熔断器工厂
/// </summary>
public interface ICircuitBreakerFactory
{
    /// <summary>
    /// 获取或创建熔断器
    /// </summary>
    /// <param name="name">熔断器名称</param>
    /// <param name="options">配置选项</param>
    ICircuitBreaker GetOrCreate(string name, CircuitBreakerOptions? options = null);
}

/// <summary>
/// 熔断器状态
/// </summary>
public enum CircuitBreakerState
{
    /// <summary>
    /// 关闭（正常运行）
    /// </summary>
    Closed,

    /// <summary>
    /// 打开（熔断中）
    /// </summary>
    Open,

    /// <summary>
    /// 半开（尝试恢复）
    /// </summary>
    HalfOpen
}

/// <summary>
/// 熔断器配置
/// </summary>
public sealed record CircuitBreakerOptions
{
    /// <summary>
    /// 失败阈值（触发熔断的失败次数）
    /// </summary>
    public int FailureThreshold { get; init; } = 5;

    /// <summary>
    /// 成功阈值（从半开恢复的成功次数）
    /// </summary>
    public int SuccessThreshold { get; init; } = 3;

    /// <summary>
    /// 采样时间窗口
    /// </summary>
    public TimeSpan SamplingDuration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 熔断持续时间
    /// </summary>
    public TimeSpan BreakDuration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 失败率阈值（0-1）
    /// </summary>
    public double FailureRateThreshold { get; init; } = 0.5;

    /// <summary>
    /// 最小请求数（低于此值不触发熔断）
    /// </summary>
    public int MinimumThroughput { get; init; } = 10;

    /// <summary>
    /// 需要熔断的异常类型
    /// </summary>
    public HashSet<Type> HandledExceptions { get; init; } = [
        typeof(HttpRequestException),
        typeof(TimeoutException)
    ];
}
