namespace JZVerse.MicroHuaxia.Saga.Abstractions.Models;

/// <summary>
/// Saga 上下文数据
/// </summary>
public sealed class SagaContext
{
    /// <summary>
    /// 全局共享数据（所有步骤可读写）
    /// </summary>
    public Dictionary<string, object?> Data { get; set; } = new();
    
    /// <summary>
    /// 每个步骤的输出结果
    /// </summary>
    public Dictionary<string, object?> StepResults { get; set; } = new();
    
    /// <summary>
    /// 获取数据
    /// </summary>
    public T? Get<T>(string key)
    {
        if (Data.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default;
    }
    
    /// <summary>
    /// 设置数据
    /// </summary>
    public void Set(string key, object? value)
    {
        Data[key] = value;
    }
    
    /// <summary>
    /// 获取步骤结果
    /// </summary>
    public T? GetStepResult<T>(string stepId)
    {
        if (StepResults.TryGetValue(stepId, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default;
    }
    
    /// <summary>
    /// 设置当前步骤的结果
    /// </summary>
    public void SetStepResult(string stepId, object? result)
    {
        StepResults[stepId] = result;
    }
}
