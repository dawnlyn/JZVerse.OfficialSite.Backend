namespace JZVerse.MicroHuaxia.ServiceCommunication.Abstractions.Resilience;

/// <summary>
/// 超时策略接口
/// </summary>
public interface ITimeoutPolicy
{
    /// <summary>
    /// 执行带超时保护的操作
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="action">操作</param>
    /// <param name="timeout">超时时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 弹性策略管道 - 组合多个策略
/// </summary>
public interface IResiliencePipeline
{
    /// <summary>
    /// 执行带弹性保护的操作
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="action">操作</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行带弹性保护的操作（无返回值）
    /// </summary>
    Task ExecuteAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 弹性策略管道工厂
/// </summary>
public interface IResiliencePipelineFactory
{
    /// <summary>
    /// 获取或创建弹性策略管道
    /// </summary>
    /// <param name="serviceName">服务名称</param>
    IResiliencePipeline GetOrCreate(string serviceName);
}
