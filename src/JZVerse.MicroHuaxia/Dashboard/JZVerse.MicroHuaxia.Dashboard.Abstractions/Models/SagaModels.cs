namespace JZVerse.MicroHuaxia.Dashboard.Abstractions.Models;

/// <summary>
/// Saga 实例信息
/// </summary>
public class SagaInstanceInfo
{
    public string InstanceId { get; set; } = string.Empty;
    public string SagaId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int CurrentStepIndex { get; set; }
    public int TotalSteps { get; set; }
    public string CompensationStrategy { get; set; } = string.Empty;
    public List<SagaStepInfo> Steps { get; set; } = [];
}

/// <summary>
/// Saga 步骤信息
/// </summary>
public class SagaStepInfo
{
    public string StepId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsCompensable { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Saga 统计信息
/// </summary>
public class SagaStats
{
    public int TotalInstances { get; set; }
    public int Executing { get; set; }
    public int Completed { get; set; }
    public int Failed { get; set; }
    public int Compensating { get; set; }
    public int Compensated { get; set; }
    public int TimedOut { get; set; }
}
