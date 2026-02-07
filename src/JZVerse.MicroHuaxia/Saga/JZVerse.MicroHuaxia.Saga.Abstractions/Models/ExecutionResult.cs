namespace JZVerse.MicroHuaxia.Saga.Abstractions.Models;

/// <summary>
/// 步骤执行结果
/// </summary>
public sealed record StepExecutionResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// 结果数据
    /// </summary>
    public object? Data { get; init; }
    
    /// <summary>
    /// 错误消息
    /// </summary>
    public string? Error { get; init; }
    
    /// <summary>
    /// 是否可重试
    /// </summary>
    public bool Retryable { get; init; }
    
    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static StepExecutionResult Succeeded(object? data = null) => new()
    {
        Success = true,
        Data = data
    };
    
    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static StepExecutionResult Failed(string error, bool retryable = false) => new()
    {
        Success = false,
        Error = error,
        Retryable = retryable
    };
}

/// <summary>
/// 补偿执行结果
/// </summary>
public sealed record CompensationResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// 错误消息
    /// </summary>
    public string? Error { get; init; }
    
    /// <summary>
    /// 是否可重试
    /// </summary>
    public bool Retryable { get; init; }
    
    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static CompensationResult Succeeded() => new() { Success = true };
    
    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static CompensationResult Failed(string error, bool retryable = false) => new()
    {
        Success = false,
        Error = error,
        Retryable = retryable
    };
}

/// <summary>
/// 重试策略
/// </summary>
public sealed record RetryPolicy
{
    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; init; } = 3;
    
    /// <summary>
    /// 初始重试延迟
    /// </summary>
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(1);
    
    /// <summary>
    /// 最大重试延迟
    /// </summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// 延迟倍数（指数退避）
    /// </summary>
    public double BackoffMultiplier { get; init; } = 2.0;
    
    /// <summary>
    /// 计算第 n 次重试的延迟
    /// </summary>
    public TimeSpan GetDelay(int retryAttempt)
    {
        var delay = TimeSpan.FromMilliseconds(
            InitialDelay.TotalMilliseconds * Math.Pow(BackoffMultiplier, retryAttempt));
        return delay > MaxDelay ? MaxDelay : delay;
    }
}
