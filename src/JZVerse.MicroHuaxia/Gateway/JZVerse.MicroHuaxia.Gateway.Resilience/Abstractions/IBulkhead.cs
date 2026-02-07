namespace JZVerse.MicroHuaxia.Gateway.Resilience.Abstractions;

/// <summary>
/// 舱壁隔离器接口
/// </summary>
public interface IBulkhead
{
    /// <summary>
    /// 舱壁名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 最大并发数
    /// </summary>
    int MaxConcurrency { get; }

    /// <summary>
    /// 当前并发数
    /// </summary>
    int CurrentConcurrency { get; }

    /// <summary>
    /// 最大队列长度
    /// </summary>
    int MaxQueueLength { get; }

    /// <summary>
    /// 当前队列长度
    /// </summary>
    int CurrentQueueLength { get; }

    /// <summary>
    /// 执行带舱壁保护的操作
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="action">操作</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行带舱壁保护的操作（无返回值）
    /// </summary>
    Task ExecuteAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 舱壁工厂接口
/// </summary>
public interface IBulkheadFactory
{
    /// <summary>
    /// 获取或创建舱壁
    /// </summary>
    /// <param name="name">舱壁名称</param>
    /// <param name="options">配置选项</param>
    IBulkhead GetOrCreate(string name, BulkheadOptions? options = null);
}

/// <summary>
/// 舱壁配置选项
/// </summary>
public sealed record BulkheadOptions
{
    /// <summary>
    /// 最大并发请求数
    /// </summary>
    public int MaxConcurrency { get; init; } = 100;

    /// <summary>
    /// 最大队列长度（超过则拒绝）
    /// </summary>
    public int MaxQueueLength { get; init; } = 100;

    /// <summary>
    /// 队列超时时间
    /// </summary>
    public TimeSpan QueueTimeout { get; init; } = TimeSpan.FromSeconds(10);
}

/// <summary>
/// 舱壁拒绝异常
/// </summary>
public sealed class BulkheadRejectedException : Exception
{
    public BulkheadRejectedException(string bulkheadName)
        : base($"Bulkhead '{bulkheadName}' rejected the request due to capacity limits.")
    {
        BulkheadName = bulkheadName;
    }

    public BulkheadRejectedException(string bulkheadName, string message)
        : base(message)
    {
        BulkheadName = bulkheadName;
    }

    /// <summary>
    /// 舱壁名称
    /// </summary>
    public string BulkheadName { get; }
}
