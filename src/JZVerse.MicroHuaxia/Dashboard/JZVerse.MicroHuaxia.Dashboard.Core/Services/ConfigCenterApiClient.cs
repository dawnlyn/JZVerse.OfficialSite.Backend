using System.Net.Http.Json;
using JZVerse.MicroHuaxia.Dashboard.Abstractions.Configuration;
using JZVerse.MicroHuaxia.Dashboard.Abstractions.Models;
using JZVerse.MicroHuaxia.Dashboard.Abstractions.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Dashboard.Core.Services;

/// <summary>
/// 配置中心 API 客户端实现
/// </summary>
public class ConfigCenterApiClient : IConfigCenterApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ConfigCenterApiClient> _logger;

    public ConfigCenterApiClient(
        HttpClient httpClient,
        IOptions<DashboardOptions> options,
        ILogger<ConfigCenterApiClient> logger)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(options.Value.ServiceEndpoints.ConfigCenter);
        _logger = logger;
    }

    public async Task<ConfigCenterStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var namespaces = await GetNamespacesAsync(cancellationToken);
            var totalItems = namespaces.Sum(n => n.ItemCount);

            return new ConfigCenterStats
            {
                TotalNamespaces = namespaces.Count,
                TotalConfigItems = totalItems,
                GrayReleaseCount = 0 // TODO: 获取灰度发布数量
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取配置中心统计数据失败");
            return new ConfigCenterStats();
        }
    }

    public async Task<List<ConfigNamespace>> GetNamespacesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/v1/config/namespaces", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new List<ConfigNamespace>();
            }

            var result = await response.Content.ReadFromJsonAsync<NamespaceListResponse>(cancellationToken);
            return result?.Namespaces?.Select(n => new ConfigNamespace
            {
                Id = n.Id,
                Name = n.Name,
                Description = n.Description,
                ItemCount = n.ItemCount
            }).ToList() ?? new List<ConfigNamespace>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取命名空间列表失败");
            return new List<ConfigNamespace>();
        }
    }

    public async Task<List<ConfigItem>> GetConfigItemsAsync(string namespaceId, string? environmentId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"/api/v1/config/namespaces/{Uri.EscapeDataString(namespaceId)}/items";
            if (!string.IsNullOrEmpty(environmentId))
            {
                url += $"?environmentId={Uri.EscapeDataString(environmentId)}";
            }

            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new List<ConfigItem>();
            }

            var result = await response.Content.ReadFromJsonAsync<ConfigItemListResponse>(cancellationToken);
            return result?.Items?.Select(MapToConfigItem).ToList() ?? new List<ConfigItem>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取命名空间 {NamespaceId} 的配置项失败", namespaceId);
            return new List<ConfigItem>();
        }
    }

    public async Task<ConfigItem?> GetConfigItemAsync(string itemId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/v1/config/items/{Uri.EscapeDataString(itemId)}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var dto = await response.Content.ReadFromJsonAsync<ConfigItemDto>(cancellationToken);
            return dto != null ? MapToConfigItem(dto) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取配置项 {ItemId} 失败", itemId);
            return null;
        }
    }

    public async Task<ConfigItem> UpsertConfigItemAsync(ConfigItem item, CancellationToken cancellationToken = default)
    {
        try
        {
            var dto = new ConfigItemDto
            {
                Id = item.Id,
                Key = item.Key,
                Value = item.Value,
                NamespaceId = item.NamespaceId,
                EnvironmentId = item.EnvironmentId,
                ValueType = item.ValueType.ToString(),
                Description = item.Description
            };

            var response = await _httpClient.PostAsJsonAsync("/api/v1/config/items", dto, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ConfigItemDto>(cancellationToken);
            return result != null ? MapToConfigItem(result) : item;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建或更新配置项失败");
            throw;
        }
    }

    public async Task DeleteConfigItemAsync(string itemId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/v1/config/items/{Uri.EscapeDataString(itemId)}", cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除配置项 {ItemId} 失败", itemId);
            throw;
        }
    }

    private static ConfigItem MapToConfigItem(ConfigItemDto dto)
    {
        return new ConfigItem
        {
            Id = dto.Id,
            Key = dto.Key,
            Value = dto.Value,
            NamespaceId = dto.NamespaceId,
            EnvironmentId = dto.EnvironmentId,
            ValueType = Enum.TryParse<ConfigValueType>(dto.ValueType, true, out var type) ? type : ConfigValueType.String,
            Description = dto.Description,
            Version = dto.Version,
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt
        };
    }

    // 内部 DTO 类型
    private class NamespaceListResponse
    {
        public List<NamespaceDto>? Namespaces { get; set; }
    }

    private class NamespaceDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int ItemCount { get; set; }
    }

    private class ConfigItemListResponse
    {
        public List<ConfigItemDto>? Items { get; set; }
    }

    private class ConfigItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string NamespaceId { get; set; } = string.Empty;
        public string EnvironmentId { get; set; } = string.Empty;
        public string ValueType { get; set; } = "String";
        public string? Description { get; set; }
        public int Version { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
