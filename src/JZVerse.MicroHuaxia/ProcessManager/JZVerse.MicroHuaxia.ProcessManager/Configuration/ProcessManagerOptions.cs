using JZVerse.MicroHuaxia.ProcessManager.Models;

namespace JZVerse.MicroHuaxia.ProcessManager.Configuration;

/// <summary>
/// ProcessManager 配置选项
/// </summary>
public class ProcessManagerOptions
{
    public const string SectionName = "ProcessManager";

    /// <summary>
    /// 预置的进程定义列表（从配置文件加载）
    /// </summary>
    public List<ManagedProcessDefinition> Processes { get; set; } = [];

    /// <summary>
    /// 停止进程时的等待超时（秒）
    /// </summary>
    public int StopTimeoutSeconds { get; set; } = 15;
}
