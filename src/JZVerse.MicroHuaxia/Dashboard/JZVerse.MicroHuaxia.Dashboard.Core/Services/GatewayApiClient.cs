using System.Net.Http.Json;
using JZVerse.MicroHuaxia.Dashboard.Abstractions.Configuration;
using JZVerse.MicroHuaxia.Dashboard.Abstractions.Models;
using JZVerse.MicroHuaxia.Dashboard.Abstractions.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Dashboard.Core.Services;

/// <summary>
/// 网关 API 客户端实现
/// </summary>
public class GatewayApiClient : IGatewayApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GatewayApiClient> _logger;

    public GatewayApiClient(
        HttpClient httpClient,
        IOptions<DashboardOptions> options,
        ILogger<GatewayApiClient> logger)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(options.Value.ServiceEndpoints.Gateway);
        _logger = logger;
    }

    public async Task<GatewayStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/v1/gateway/metrics", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // 如果没有 metrics 端点，返回基础统计
                var routes = await GetRoutesAsync(cancellationToken);
                return new GatewayStats
                {
                    TotalRoutes = routes.Count
                };
            }

            var result = await response.Content.ReadFromJsonAsync<MetricsDto>(cancellationToken);
            return new GatewayStats
            {
                TotalRoutes = result?.TotalRoutes ?? 0,
                RequestsPerSecond = result?.RequestsPerSecond ?? 0,
                AverageResponseTimeMs = result?.AverageResponseTimeMs ?? 0,
                ErrorRate = result?.ErrorRate ?? 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取网关统计数据失败");
            return new GatewayStats();
        }
    }

    public async Task<List<RouteInfo>> GetRoutesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/v1/gateway/routes", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new List<RouteInfo>();
            }

            // 服务端直接返回 GatewayRoute 数组
            var routes = await response.Content.ReadFromJsonAsync<List<GatewayRouteDto>>(cancellationToken);
            return routes?.Select(MapToRouteInfo).ToList() ?? new List<RouteInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取路由列表失败");
            return new List<RouteInfo>();
        }
    }

    public async Task<RouteInfo?> GetRouteAsync(string routeId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/v1/gateway/routes/{Uri.EscapeDataString(routeId)}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var dto = await response.Content.ReadFromJsonAsync<GatewayRouteDto>(cancellationToken);
            return dto != null ? MapToRouteInfo(dto) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取路由 {RouteId} 失败", routeId);
            return null;
        }
    }

    public async Task<RouteInfo> UpsertRouteAsync(RouteInfo route, CancellationToken cancellationToken = default)
    {
        try
        {
            var dto = MapToGatewayRouteDto(route);

            HttpResponseMessage response;
            if (string.IsNullOrEmpty(route.Id))
            {
                response = await _httpClient.PostAsJsonAsync("/api/v1/gateway/routes", dto, cancellationToken);
            }
            else
            {
                response = await _httpClient.PutAsJsonAsync($"/api/v1/gateway/routes/{Uri.EscapeDataString(route.Id)}", dto, cancellationToken);
            }

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GatewayRouteDto>(cancellationToken);
            return result != null ? MapToRouteInfo(result) : route;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建或更新路由失败");
            throw;
        }
    }

    public async Task DeleteRouteAsync(string routeId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/v1/gateway/routes/{Uri.EscapeDataString(routeId)}", cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除路由 {RouteId} 失败", routeId);
            throw;
        }
    }

    public async Task<(List<AuditLog> Items, int Total)> QueryAuditLogsAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            // 将 Dashboard 的查询参数转换为服务端 AuditLogQuery 格式
            var serverQuery = new
            {
                From = query.StartTime.HasValue ? new DateTimeOffset(query.StartTime.Value, TimeSpan.Zero) : (DateTimeOffset?)null,
                To = query.EndTime.HasValue ? new DateTimeOffset(query.EndTime.Value, TimeSpan.Zero) : (DateTimeOffset?)null,
                StatusCode = query.StatusCode,
                Skip = (query.Page - 1) * query.PageSize,
                Take = query.PageSize
            };

            var response = await _httpClient.PostAsJsonAsync("/api/v1/gateway/audit/query", serverQuery, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return (new List<AuditLog>(), 0);
            }

            // 服务端直接返回 AuditLogEntry 数组
            var entries = await response.Content.ReadFromJsonAsync<List<AuditLogEntryDto>>(cancellationToken);
            var items = entries?.Select(MapToAuditLog).ToList() ?? new List<AuditLog>();

            // 仅业务服务日志：过滤 TargetService 有值的记录
            if (query.BusinessOnly)
            {
                items = items.Where(i => !string.IsNullOrEmpty(i.TargetService)).ToList();
            }

            return (items, items.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询审计日志失败");
            return (new List<AuditLog>(), 0);
        }
    }

    private static RouteInfo MapToRouteInfo(GatewayRouteDto dto)
    {
        return new RouteInfo
        {
            Id = dto.RouteId,
            Name = dto.RouteName,
            Path = dto.Match?.Path ?? string.Empty,
            Methods = dto.Match?.Methods ?? new List<string>(),
            TargetService = dto.Destination?.ServiceName ?? string.Empty,
            TargetPath = dto.Destination?.PathTransform ?? string.Empty,
            Enabled = dto.Enabled,
            Priority = dto.Priority,
            LoadBalancer = dto.Destination?.LoadBalancerStrategy ?? "RoundRobin",
            TimeoutSeconds = dto.Timeout.HasValue ? (int)dto.Timeout.Value.TotalSeconds : 30
        };
    }

    private static GatewayRouteDto MapToGatewayRouteDto(RouteInfo route)
    {
        return new GatewayRouteDto
        {
            RouteId = route.Id,
            RouteName = route.Name,
            Priority = route.Priority,
            Match = new RouteMatchDto
            {
                Path = route.Path,
                Methods = route.Methods
            },
            Destination = new RouteDestinationDto
            {
                ServiceName = route.TargetService,
                PathTransform = route.TargetPath,
                LoadBalancerStrategy = route.LoadBalancer
            },
            Timeout = TimeSpan.FromSeconds(route.TimeoutSeconds),
            Enabled = route.Enabled
        };
    }

    private static AuditLog MapToAuditLog(AuditLogEntryDto dto)
    {
        return new AuditLog
        {
            Id = dto.LogId,
            Timestamp = dto.Timestamp.DateTime,
            Path = dto.RequestPath,
            Method = dto.Method,
            StatusCode = dto.StatusCode,
            ResponseTimeMs = dto.DurationMs,
            ClientIp = dto.ClientIp ?? string.Empty,
            TargetService = dto.TargetService ?? string.Empty,
            ErrorMessage = dto.ErrorMessage
        };
    }

    // 内部 DTO 类型，匹配服务端实际 JSON 结构
    private class MetricsDto
    {
        public int TotalRoutes { get; set; }
        public double RequestsPerSecond { get; set; }
        public double AverageResponseTimeMs { get; set; }
        public double ErrorRate { get; set; }
    }

    private class GatewayRouteDto
    {
        public string RouteId { get; set; } = string.Empty;
        public string RouteName { get; set; } = string.Empty;
        public int Priority { get; set; } = 100;
        public RouteMatchDto? Match { get; set; }
        public RouteDestinationDto? Destination { get; set; }
        public TimeSpan? Timeout { get; set; }
        public bool Enabled { get; set; } = true;
    }

    private class RouteMatchDto
    {
        public string Path { get; set; } = string.Empty;
        public List<string> Methods { get; set; } = [];
    }

    private class RouteDestinationDto
    {
        public string? ServiceName { get; set; }
        public string? PathTransform { get; set; }
        public string LoadBalancerStrategy { get; set; } = "RoundRobin";
    }

    private class AuditLogEntryDto
    {
        public string LogId { get; set; } = string.Empty;
        public DateTimeOffset Timestamp { get; set; }
        public string RequestPath { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public long DurationMs { get; set; }
        public string? ClientIp { get; set; }
        public string? TargetService { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
