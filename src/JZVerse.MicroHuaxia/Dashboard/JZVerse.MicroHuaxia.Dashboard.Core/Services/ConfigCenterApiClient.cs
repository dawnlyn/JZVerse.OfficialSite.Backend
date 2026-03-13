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

    // ===== 灰度发布 =====

    public async Task<List<GrayReleaseInfo>> GetGrayReleasesAsync(string namespaceId, string environmentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"/api/v1/gray-releases/namespaces/{Uri.EscapeDataString(namespaceId)}/active?environmentId={Uri.EscapeDataString(environmentId)}";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new List<GrayReleaseInfo>();
            }

            var dtos = await response.Content.ReadFromJsonAsync<List<GrayReleaseDto>>(cancellationToken);
            return dtos?.Select(MapToGrayReleaseInfo).ToList() ?? new List<GrayReleaseInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取命名空间 {NamespaceId} 的灰度发布列表失败", namespaceId);
            return new List<GrayReleaseInfo>();
        }
    }

    public async Task<GrayReleaseInfo?> GetGrayReleaseAsync(string releaseId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/v1/gray-releases/{Uri.EscapeDataString(releaseId)}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var dto = await response.Content.ReadFromJsonAsync<GrayReleaseDto>(cancellationToken);
            return dto != null ? MapToGrayReleaseInfo(dto) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取灰度发布 {ReleaseId} 详情失败", releaseId);
            return null;
        }
    }

    public async Task<GrayReleaseInfo> CreateGrayReleaseAsync(CreateGrayReleaseInput input, CancellationToken cancellationToken = default)
    {
        try
        {
            var requestDto = new CreateGrayReleaseRequestDto
            {
                ReleaseName = input.ReleaseName,
                NamespaceId = input.NamespaceId,
                EnvironmentId = input.EnvironmentId,
                Strategy = MapStrategyToInt(input.Strategy),
                RolloutPercentage = input.RolloutPercentage,
                Rules = input.Rules.Select(r => new GrayReleaseRuleDto
                {
                    RuleType = MapRuleTypeToInt(r.RuleType),
                    MatchPattern = r.MatchPattern,
                    Priority = r.Priority
                }).ToList()
            };

            var response = await _httpClient.PostAsJsonAsync("/api/v1/gray-releases", requestDto, cancellationToken);
            response.EnsureSuccessStatusCode();

            var dto = await response.Content.ReadFromJsonAsync<GrayReleaseDto>(cancellationToken);
            return dto != null ? MapToGrayReleaseInfo(dto) : new GrayReleaseInfo { ReleaseName = input.ReleaseName };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建灰度发布失败");
            throw;
        }
    }

    public async Task StartGrayReleaseAsync(string releaseId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/v1/gray-releases/{Uri.EscapeDataString(releaseId)}/start", null, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "开始灰度发布 {ReleaseId} 失败", releaseId);
            throw;
        }
    }

    public async Task CompleteGrayReleaseAsync(string releaseId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/v1/gray-releases/{Uri.EscapeDataString(releaseId)}/complete", null, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "完成灰度发布 {ReleaseId} 失败", releaseId);
            throw;
        }
    }

    public async Task RollbackGrayReleaseAsync(string releaseId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/v1/gray-releases/{Uri.EscapeDataString(releaseId)}/rollback", null, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "回滚灰度发布 {ReleaseId} 失败", releaseId);
            throw;
        }
    }

    public async Task<ClientMatchTestResult?> TestGrayReleaseMatchAsync(string releaseId, ClientMatchTestInput input, CancellationToken cancellationToken = default)
    {
        try
        {
            var clientInfoDto = new ClientInfoDto
            {
                ClientId = input.ClientId,
                IpAddress = input.IpAddress,
                Tags = string.IsNullOrEmpty(input.Tags)
                    ? []
                    : [.. input.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)]
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"/api/v1/gray-releases/{Uri.EscapeDataString(releaseId)}/match",
                clientInfoDto,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var dto = await response.Content.ReadFromJsonAsync<MatchResultDto>(cancellationToken);
            return dto != null ? new ClientMatchTestResult { Matches = dto.Matches, ReleaseId = dto.ReleaseId } : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试灰度匹配失败，发布 {ReleaseId}", releaseId);
            return null;
        }
    }

    // ===== 版本历史 =====

    public async Task<List<ConfigVersionInfo>> GetConfigVersionHistoryAsync(string itemId, int limit = 50, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"/api/v1/versions/items/{Uri.EscapeDataString(itemId)}/history?limit={limit}";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new List<ConfigVersionInfo>();
            }

            var dtos = await response.Content.ReadFromJsonAsync<List<ConfigVersionDto>>(cancellationToken);
            return dtos?.Select(MapToConfigVersionInfo).ToList() ?? new List<ConfigVersionInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取配置项 {ItemId} 版本历史失败", itemId);
            return new List<ConfigVersionInfo>();
        }
    }

    public async Task RollbackConfigVersionAsync(string itemId, long targetVersion, string? reason = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var requestDto = new RollbackRequestDto
            {
                TargetVersion = targetVersion,
                Reason = reason
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"/api/v1/versions/items/{Uri.EscapeDataString(itemId)}/rollback",
                requestDto,
                cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "回滚配置项 {ItemId} 到版本 {Version} 失败", itemId, targetVersion);
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

    // ===== 灰度发布 DTO =====

    private class GrayReleaseDto
    {
        public string ReleaseId { get; set; } = string.Empty;
        public string ReleaseName { get; set; } = string.Empty;
        public string NamespaceId { get; set; } = string.Empty;
        public string EnvironmentId { get; set; } = string.Empty;
        public int Strategy { get; set; }
        public int Status { get; set; }
        public int RolloutPercentage { get; set; }
        public List<GrayReleaseRuleDto> TargetRules { get; set; } = [];
        public Dictionary<string, string> ConfigSnapshot { get; set; } = new();
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public string? CreatedBy { get; set; }
    }

    private class GrayReleaseRuleDto
    {
        public int RuleType { get; set; }
        public string MatchPattern { get; set; } = string.Empty;
        public int Priority { get; set; }
    }

    private class CreateGrayReleaseRequestDto
    {
        public string ReleaseName { get; set; } = string.Empty;
        public string NamespaceId { get; set; } = string.Empty;
        public string EnvironmentId { get; set; } = string.Empty;
        public int Strategy { get; set; }
        public int RolloutPercentage { get; set; }
        public List<GrayReleaseRuleDto> Rules { get; set; } = [];
    }

    private class ClientInfoDto
    {
        public string ClientId { get; set; } = string.Empty;
        public string? IpAddress { get; set; }
        public List<string> Tags { get; set; } = [];
    }

    private class MatchResultDto
    {
        public bool Matches { get; set; }
        public string ReleaseId { get; set; } = string.Empty;
    }

    // ===== 版本历史 DTO =====

    private class ConfigVersionDto
    {
        public string VersionId { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;
        public long Version { get; set; }
        public string? OldValue { get; set; }
        public string NewValue { get; set; } = string.Empty;
        public int ChangeType { get; set; }
        public string? Reason { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public long? RollbackFromVersion { get; set; }
    }

    private class RollbackRequestDto
    {
        public long TargetVersion { get; set; }
        public string? Reason { get; set; }
    }

    // ===== 映射方法 =====

    private static GrayReleaseInfo MapToGrayReleaseInfo(GrayReleaseDto dto)
    {
        return new GrayReleaseInfo
        {
            ReleaseId = dto.ReleaseId,
            ReleaseName = dto.ReleaseName,
            NamespaceId = dto.NamespaceId,
            EnvironmentId = dto.EnvironmentId,
            Strategy = MapStrategyFromInt(dto.Strategy),
            Status = MapStatusFromInt(dto.Status),
            RolloutPercentage = dto.RolloutPercentage,
            TargetRules = dto.TargetRules.Select(r => new GrayRuleInfo
            {
                RuleType = MapRuleTypeFromInt(r.RuleType),
                MatchPattern = r.MatchPattern,
                Priority = r.Priority
            }).ToList(),
            ConfigSnapshot = dto.ConfigSnapshot,
            CreatedAt = dto.CreatedAt.LocalDateTime,
            StartedAt = dto.StartedAt?.LocalDateTime,
            CompletedAt = dto.CompletedAt?.LocalDateTime,
            CreatedBy = dto.CreatedBy
        };
    }

    private static ConfigVersionInfo MapToConfigVersionInfo(ConfigVersionDto dto)
    {
        return new ConfigVersionInfo
        {
            VersionId = dto.VersionId,
            ItemId = dto.ItemId,
            Version = dto.Version,
            OldValue = dto.OldValue,
            NewValue = dto.NewValue,
            ChangeType = MapChangeTypeFromInt(dto.ChangeType),
            Reason = dto.Reason,
            CreatedAt = dto.CreatedAt.LocalDateTime,
            CreatedBy = dto.CreatedBy,
            RollbackFromVersion = dto.RollbackFromVersion
        };
    }

    // int → string 映射（后端 enum → 前端字符串）

    private static string MapStrategyFromInt(int strategy) => strategy switch
    {
        0 => "Manual",
        1 => "Automatic",
        2 => "Canary",
        _ => "Unknown"
    };

    private static string MapStatusFromInt(int status) => status switch
    {
        0 => "Draft",
        1 => "InProgress",
        2 => "Completed",
        3 => "Rollback",
        4 => "Cancelled",
        _ => "Unknown"
    };

    private static string MapRuleTypeFromInt(int ruleType) => ruleType switch
    {
        0 => "IP",
        1 => "Tag",
        2 => "ClientId",
        3 => "Percentage",
        _ => "Unknown"
    };

    private static string MapChangeTypeFromInt(int changeType) => changeType switch
    {
        0 => "Created",
        1 => "Updated",
        2 => "Deleted",
        3 => "Rollback",
        _ => "Unknown"
    };

    // string → int 映射（前端字符串 → 后端 enum）

    private static int MapStrategyToInt(string strategy) => strategy switch
    {
        "Manual" => 0,
        "Automatic" => 1,
        "Canary" => 2,
        _ => 0
    };

    private static int MapRuleTypeToInt(string ruleType) => ruleType switch
    {
        "IP" => 0,
        "Tag" => 1,
        "ClientId" => 2,
        "Percentage" => 3,
        _ => 0
    };
}
