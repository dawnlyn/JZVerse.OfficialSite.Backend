using System.Net.Http.Json;
using JZVerse.MicroHuaxia.Dashboard.Abstractions.Configuration;
using JZVerse.MicroHuaxia.Dashboard.Abstractions.Models;
using JZVerse.MicroHuaxia.Dashboard.Abstractions.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Dashboard.Core.Services;

/// <summary>
/// Saga API 客户端实现
/// </summary>
public class SagaApiClient : ISagaApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SagaApiClient> _logger;

    public SagaApiClient(
        HttpClient httpClient,
        IOptions<DashboardOptions> options,
        ILogger<SagaApiClient> logger)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(options.Value.ServiceEndpoints.Saga);
        _logger = logger;
    }

    public async Task<SagaStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var dto = await _httpClient.GetFromJsonAsync<SagaStatsDto>("/api/v1/saga/stats", cancellationToken);
            return new SagaStats
            {
                TotalInstances = dto?.TotalInstances ?? 0,
                Executing = dto?.Executing ?? 0,
                Completed = dto?.Completed ?? 0,
                Failed = dto?.Failed ?? 0,
                Compensating = dto?.Compensating ?? 0,
                Compensated = dto?.Compensated ?? 0,
                TimedOut = dto?.TimedOut ?? 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 Saga 统计数据失败");
            return new SagaStats();
        }
    }

    public async Task<List<SagaInstanceInfo>> QueryInstancesAsync(
        string? sagaId = null,
        string? status = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var queryParts = new List<string> { $"limit={limit}" };
            if (!string.IsNullOrEmpty(sagaId))
                queryParts.Add($"sagaId={Uri.EscapeDataString(sagaId)}");
            if (!string.IsNullOrEmpty(status))
                queryParts.Add($"status={Uri.EscapeDataString(status)}");

            var url = $"/api/v1/saga/instances?{string.Join("&", queryParts)}";
            var dtos = await _httpClient.GetFromJsonAsync<List<SagaInstanceDto>>(url, cancellationToken);
            return dtos?.Select(MapToSagaInstanceInfo).ToList() ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询 Saga 实例列表失败");
            return [];
        }
    }

    public async Task<SagaInstanceInfo?> GetInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/api/v1/saga/instances/{Uri.EscapeDataString(instanceId)}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var dto = await response.Content.ReadFromJsonAsync<SagaInstanceDto>(cancellationToken);
            return dto != null ? MapToSagaInstanceInfo(dto) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 Saga 实例 {InstanceId} 详情失败", instanceId);
            return null;
        }
    }

    public async Task CompensateAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"/api/v1/saga/instances/{Uri.EscapeDataString(instanceId)}/compensate", null, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "触发 Saga {InstanceId} 补偿失败", instanceId);
            throw;
        }
    }

    public async Task CancelAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"/api/v1/saga/instances/{Uri.EscapeDataString(instanceId)}/cancel", null, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取消 Saga {InstanceId} 失败", instanceId);
            throw;
        }
    }

    public async Task ResumeAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"/api/v1/saga/instances/{Uri.EscapeDataString(instanceId)}/resume", null, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "恢复 Saga {InstanceId} 执行失败", instanceId);
            throw;
        }
    }

    private static SagaInstanceInfo MapToSagaInstanceInfo(SagaInstanceDto dto)
    {
        return new SagaInstanceInfo
        {
            InstanceId = dto.InstanceId,
            SagaId = dto.SagaId,
            Name = dto.Name,
            Status = dto.Status.ToString(),
            CreatedAt = dto.CreatedAt.DateTime,
            StartedAt = dto.StartedAt?.DateTime,
            CompletedAt = dto.CompletedAt?.DateTime,
            ErrorMessage = dto.ErrorMessage,
            CurrentStepIndex = dto.CurrentStepIndex,
            TotalSteps = dto.Steps?.Count ?? 0,
            CompensationStrategy = dto.Strategy.ToString(),
            Steps = dto.Steps?.Select(s => new SagaStepInfo
            {
                StepId = s.StepId,
                Name = s.Name,
                Order = s.Order,
                Status = s.Status.ToString(),
                IsCompensable = s.IsCompensable,
                StartedAt = s.StartedAt?.DateTime,
                CompletedAt = s.CompletedAt?.DateTime,
                RetryCount = s.RetryCount,
                ErrorMessage = s.ErrorMessage
            }).ToList() ?? []
        };
    }

    // 内部 DTO 类型，匹配 Saga 服务端 JSON 结构
    private class SagaStatsDto
    {
        public int TotalInstances { get; set; }
        public int Executing { get; set; }
        public int Completed { get; set; }
        public int Failed { get; set; }
        public int Compensating { get; set; }
        public int Compensated { get; set; }
        public int TimedOut { get; set; }
    }

    private class SagaInstanceDto
    {
        public string InstanceId { get; set; } = string.Empty;
        public string SagaId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Status { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public string? ErrorMessage { get; set; }
        public int CurrentStepIndex { get; set; }
        public int Strategy { get; set; }
        public List<SagaStepDto>? Steps { get; set; }
    }

    private class SagaStepDto
    {
        public string StepId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Order { get; set; }
        public int Status { get; set; }
        public bool IsCompensable { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public int RetryCount { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
