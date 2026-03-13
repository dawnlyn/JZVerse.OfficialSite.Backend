namespace JZVerse.MicroHuaxia.ProcessManager.Models;

/// <summary>
/// 托管进程定义（启动配置）
/// </summary>
public class ManagedProcessDefinition
{
    /// <summary>
    /// 进程唯一标识
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 项目路径（.csproj 文件路径，相对于工作目录）
    /// </summary>
    public string? ProjectPath { get; set; }

    /// <summary>
    /// 可执行文件路径（与 ProjectPath 互斥）
    /// </summary>
    public string? ExecutablePath { get; set; }

    /// <summary>
    /// 命令行参数
    /// </summary>
    public string? Arguments { get; set; }

    /// <summary>
    /// 工作目录
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// 环境变量
    /// </summary>
    public Dictionary<string, string> Environment { get; set; } = new();

    /// <summary>
    /// 启动顺序（数字越小越先启动）
    /// </summary>
    public int StartOrder { get; set; }
}

/// <summary>
/// 托管进程运行时状态
/// </summary>
public class ManagedProcessStatus
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public ProcessState State { get; set; }
    public int? Pid { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? StoppedAt { get; set; }
    public TimeSpan? Uptime => State == ProcessState.Running && StartedAt.HasValue
        ? DateTime.UtcNow - StartedAt.Value
        : null;
    public string? LastError { get; set; }
}

/// <summary>
/// 进程状态枚举
/// </summary>
public enum ProcessState
{
    Stopped = 0,
    Starting = 1,
    Running = 2,
    Stopping = 3,
    Failed = 4,
}
