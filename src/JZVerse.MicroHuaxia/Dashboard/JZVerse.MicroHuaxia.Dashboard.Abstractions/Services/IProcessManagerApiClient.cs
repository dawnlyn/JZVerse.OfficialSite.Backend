using JZVerse.MicroHuaxia.Dashboard.Abstractions.Models;

namespace JZVerse.MicroHuaxia.Dashboard.Abstractions.Services;

/// <summary>
/// ProcessManager API 客户端接口
/// </summary>
public interface IProcessManagerApiClient
{
    Task<List<ProcessStatusInfo>> GetAllProcessesAsync(CancellationToken ct = default);
    Task<ProcessStatusInfo?> GetProcessAsync(string processId, CancellationToken ct = default);
    Task<ProcessStatusInfo?> StartProcessAsync(string processId, CancellationToken ct = default);
    Task<ProcessStatusInfo?> StopProcessAsync(string processId, CancellationToken ct = default);
    Task<ProcessStatusInfo?> RestartProcessAsync(string processId, CancellationToken ct = default);
    Task StartAllAsync(CancellationToken ct = default);
    Task StopAllAsync(CancellationToken ct = default);
    Task RestartAllAsync(CancellationToken ct = default);
}
