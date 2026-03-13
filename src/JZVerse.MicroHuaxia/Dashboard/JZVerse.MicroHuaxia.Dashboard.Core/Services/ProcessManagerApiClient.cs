using System.Net.Http.Json;
using JZVerse.MicroHuaxia.Dashboard.Abstractions.Configuration;
using JZVerse.MicroHuaxia.Dashboard.Abstractions.Models;
using JZVerse.MicroHuaxia.Dashboard.Abstractions.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Dashboard.Core.Services;

/// <summary>
/// ProcessManager API 客户端实现
/// </summary>
public class ProcessManagerApiClient(
    HttpClient _httpClient,
    IOptions<DashboardOptions> _options,
    ILogger<ProcessManagerApiClient> _logger
) : IProcessManagerApiClient
{
    private string BaseUrl => _options.Value.ServiceEndpoints.ProcessManager;

    public async Task<List<ProcessStatusInfo>> GetAllProcessesAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/api/v1/processes", ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<ProcessStatusInfo>>(ct) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get processes from ProcessManager");
            return [];
        }
    }

    public async Task<ProcessStatusInfo?> GetProcessAsync(string processId, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{BaseUrl}/api/v1/processes/{Uri.EscapeDataString(processId)}", ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<ProcessStatusInfo>(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get process {Id}", processId);
            return null;
        }
    }

    public async Task<ProcessStatusInfo?> StartProcessAsync(string processId, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"{BaseUrl}/api/v1/processes/{Uri.EscapeDataString(processId)}/start", null, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ProcessStatusInfo>(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start process {Id}", processId);
            return null;
        }
    }

    public async Task<ProcessStatusInfo?> StopProcessAsync(string processId, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"{BaseUrl}/api/v1/processes/{Uri.EscapeDataString(processId)}/stop", null, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ProcessStatusInfo>(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop process {Id}", processId);
            return null;
        }
    }

    public async Task<ProcessStatusInfo?> RestartProcessAsync(string processId, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"{BaseUrl}/api/v1/processes/{Uri.EscapeDataString(processId)}/restart", null, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ProcessStatusInfo>(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart process {Id}", processId);
            return null;
        }
    }

    public async Task StartAllAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsync($"{BaseUrl}/api/v1/processes/start-all", null, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start all processes");
        }
    }

    public async Task StopAllAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsync($"{BaseUrl}/api/v1/processes/stop-all", null, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop all processes");
        }
    }

    public async Task RestartAllAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsync($"{BaseUrl}/api/v1/processes/restart-all", null, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart all processes");
        }
    }
}
