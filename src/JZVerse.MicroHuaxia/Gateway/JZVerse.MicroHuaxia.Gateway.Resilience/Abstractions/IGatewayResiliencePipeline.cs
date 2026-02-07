using JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;

namespace JZVerse.MicroHuaxia.Gateway.Resilience.Abstractions;

/// <summary>
/// Gateway 弹性管道接口
/// 组合执行顺序：舱壁 → 重试 → 熔断 → 超时 → 降级
/// </summary>
public interface IGatewayResiliencePipeline
{
    /// <summary>
    /// 执行带完整弹性保护的操作
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="context">弹性上下文</param>
    /// <param name="action">操作</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<T> ExecuteAsync<T>(
        IGatewayResilienceContext context,
        Func<IGatewayResilienceContext, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行带完整弹性保护的操作（无返回值）
    /// </summary>
    Task ExecuteAsync(
        IGatewayResilienceContext context,
        Func<IGatewayResilienceContext, CancellationToken, Task> action,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Gateway 弹性管道工厂接口
/// </summary>
public interface IGatewayResiliencePipelineFactory
{
    /// <summary>
    /// 为指定路由获取或创建弹性管道
    /// </summary>
    /// <param name="route">路由配置</param>
    IGatewayResiliencePipeline GetOrCreate(GatewayRoute route);
}

/// <summary>
/// 弹性执行结果
/// </summary>
public sealed record ResilienceExecutionResult<T>
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 结果值
    /// </summary>
    public T? Value { get; init; }

    /// <summary>
    /// 异常（如果失败）
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// 是否使用了降级
    /// </summary>
    public bool UsedFallback { get; init; }

    /// <summary>
    /// 尝试次数
    /// </summary>
    public int AttemptCount { get; init; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static ResilienceExecutionResult<T> Success(T value, int attemptCount)
    {
        return new ResilienceExecutionResult<T>
        {
            IsSuccess = true,
            Value = value,
            AttemptCount = attemptCount
        };
    }

    /// <summary>
    /// 创建降级结果
    /// </summary>
    public static ResilienceExecutionResult<T> Fallback(T value, int attemptCount, Exception? exception = null)
    {
        return new ResilienceExecutionResult<T>
        {
            IsSuccess = true,
            Value = value,
            UsedFallback = true,
            AttemptCount = attemptCount,
            Exception = exception
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static ResilienceExecutionResult<T> Failure(Exception exception, int attemptCount)
    {
        return new ResilienceExecutionResult<T>
        {
            IsSuccess = false,
            Exception = exception,
            AttemptCount = attemptCount
        };
    }
}
