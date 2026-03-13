namespace JZVerse.MicroHuaxia.Dashboard.Abstractions.Models;

/// <summary>
/// 托管进程状态
/// </summary>
public class ProcessStatusInfo
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string State { get; set; } = "Stopped";
    public int? Pid { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? StoppedAt { get; set; }
    public string? Uptime { get; set; }
    public string? LastError { get; set; }
}
