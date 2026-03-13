using System.Net.Http.Json;
using System.Text.Json;
using JZVerse.MicroHuaxia.Dashboard.Abstractions.Configuration;
using JZVerse.MicroHuaxia.Dashboard.Abstractions.Models;
using JZVerse.MicroHuaxia.Dashboard.Abstractions.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Dashboard.Core.Services;

/// <summary>
/// 配置迁移服务实现
/// </summary>
public class ConfigMigrationService : IConfigMigrationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfigCenterApiClient _configCenterClient;
    private readonly DashboardOptions _options;
    private readonly ILogger<ConfigMigrationService> _logger;

    /// <summary>
    /// 预定义的迁移配置文件
    /// </summary>
    private static readonly List<ConfigMigrationProfile> Profiles =
    [
        new()
        {
            ServiceName = "ServiceDiscovery",
            DisplayName = "服务发现",
            TargetNamespace = "service-discovery",
            BootstrapPrefixes =
            [
                "Kestrel", "urls", "Logging.LogLevel.Microsoft",
                "ConfigCenter.Client.ServerUrls", "ConfigCenter.Client.ApplicationId"
            ]
        },
        new()
        {
            ServiceName = "Gateway",
            DisplayName = "API 网关",
            TargetNamespace = "gateway",
            BootstrapPrefixes =
            [
                "Kestrel", "urls", "Gateway.ListenPorts",
                "ConfigCenter.Client.ServerUrls", "ConfigCenter.Client.ApplicationId"
            ]
        },
        new()
        {
            ServiceName = "MessageQueue",
            DisplayName = "消息队列",
            TargetNamespace = "message-queue",
            BootstrapPrefixes =
            [
                "Kestrel", "urls",
                "ConfigCenter.Client.ServerUrls", "ConfigCenter.Client.ApplicationId"
            ]
        },
        new()
        {
            ServiceName = "Saga",
            DisplayName = "Saga 编排",
            TargetNamespace = "saga",
            BootstrapPrefixes =
            [
                "Kestrel", "urls",
                "ConfigCenter.Client.ServerUrls", "ConfigCenter.Client.ApplicationId"
            ]
        },
    ];

    /// <summary>
    /// 各服务的配置端点
    /// </summary>
    private static readonly Dictionary<string, string> ConfigPaths = new()
    {
        ["ServiceDiscovery"] = "/api/v1/services/config",
        ["Gateway"] = "/api/v1/gateway/config",
        ["MessageQueue"] = "/api/v1/mq/config",
        ["Saga"] = "/api/v1/saga/config",
    };

    public ConfigMigrationService(
        IHttpClientFactory httpClientFactory,
        IConfigCenterApiClient configCenterClient,
        IOptions<DashboardOptions> options,
        ILogger<ConfigMigrationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configCenterClient = configCenterClient;
        _options = options.Value;
        _logger = logger;
    }

    public List<ConfigMigrationProfile> GetProfiles() => Profiles;

    public async Task<List<ConfigMigrationPreviewItem>> PreviewAsync(
        string serviceName, CancellationToken ct = default)
    {
        var profile = Profiles.Find(p => p.ServiceName == serviceName);
        if (profile is null)
            return [];

        var entries = await FetchServiceConfigAsync(serviceName, ct);
        return entries.Select(e => new ConfigMigrationPreviewItem
        {
            Key = e.Key,
            Value = e.Value,
            ValueType = e.ValueType,
            IsBootstrap = IsBootstrap(e.Key, profile.BootstrapPrefixes)
        }).ToList();
    }

    public async Task<ConfigMigrationResult> MigrateAsync(
        string serviceName, CancellationToken ct = default)
    {
        var profile = Profiles.Find(p => p.ServiceName == serviceName);
        if (profile is null)
        {
            return new ConfigMigrationResult
            {
                ServiceName = serviceName,
                Errors = [$"未找到服务 {serviceName} 的迁移配置"]
            };
        }

        var result = new ConfigMigrationResult
        {
            ServiceName = serviceName,
            TargetNamespace = profile.TargetNamespace,
        };

        List<ServiceConfigEntry> entries;
        try
        {
            entries = await FetchServiceConfigAsync(serviceName, ct);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"获取服务配置失败: {ex.Message}");
            return result;
        }

        result.TotalItems = entries.Count;

        var toMigrate = entries
            .Where(e => !IsBootstrap(e.Key, profile.BootstrapPrefixes))
            .ToList();

        result.SkippedBootstrap = result.TotalItems - toMigrate.Count;

        foreach (var entry in toMigrate)
        {
            try
            {
                var item = new ConfigItem
                {
                    Key = entry.Key,
                    Value = entry.Value,
                    NamespaceId = profile.TargetNamespace,
                    EnvironmentId = profile.TargetEnvironment,
                    ValueType = Enum.TryParse<ConfigValueType>(entry.ValueType, true, out var vt) ? vt : ConfigValueType.String,
                    Description = $"从 {profile.DisplayName} appsettings.json 迁移"
                };

                await _configCenterClient.UpsertConfigItemAsync(item, ct);
                result.MigratedItems++;
            }
            catch (Exception ex)
            {
                result.FailedItems++;
                result.Errors.Add($"迁移 {entry.Key} 失败: {ex.Message}");
                _logger.LogError(ex, "Failed to migrate config key {Key} for service {Service}",
                    entry.Key, serviceName);
            }
        }

        _logger.LogInformation(
            "Migration completed for {Service}: {Migrated}/{Total} migrated, {Skipped} bootstrap skipped, {Failed} failed",
            serviceName, result.MigratedItems, result.TotalItems, result.SkippedBootstrap, result.FailedItems);

        return result;
    }

    private async Task<List<ServiceConfigEntry>> FetchServiceConfigAsync(
        string serviceName, CancellationToken ct)
    {
        var baseUrl = GetServiceEndpoint(serviceName);
        var configPath = ConfigPaths.GetValueOrDefault(serviceName);
        if (configPath is null)
            return [];

        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(baseUrl);

        var response = await client.GetAsync(configPath, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        return FlattenJson(json, "");
    }

    private string GetServiceEndpoint(string serviceKey) => serviceKey switch
    {
        "ServiceDiscovery" => _options.ServiceEndpoints.ServiceDiscovery,
        "Gateway" => _options.ServiceEndpoints.Gateway,
        "MessageQueue" => _options.ServiceEndpoints.MessageQueue,
        "Saga" => _options.ServiceEndpoints.Saga,
        _ => _options.ServiceEndpoints.ServiceDiscovery
    };

    private static bool IsBootstrap(string key, List<string> bootstrapPrefixes)
    {
        return bootstrapPrefixes.Any(prefix =>
            key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static List<ServiceConfigEntry> FlattenJson(JsonElement element, string prefix)
    {
        var entries = new List<ServiceConfigEntry>();

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
                    if (prop.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        entries.AddRange(FlattenJson(prop.Value, key));
                    }
                    else
                    {
                        entries.Add(new ServiceConfigEntry
                        {
                            Key = key,
                            Value = prop.Value.ToString(),
                            ValueType = GetValueTypeName(prop.Value.ValueKind)
                        });
                    }
                }
                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    var key = $"{prefix}[{index}]";
                    if (item.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        entries.AddRange(FlattenJson(item, key));
                    }
                    else
                    {
                        entries.Add(new ServiceConfigEntry
                        {
                            Key = key,
                            Value = item.ToString(),
                            ValueType = GetValueTypeName(item.ValueKind)
                        });
                    }
                    index++;
                }
                break;

            default:
                entries.Add(new ServiceConfigEntry
                {
                    Key = prefix,
                    Value = element.ToString(),
                    ValueType = GetValueTypeName(element.ValueKind)
                });
                break;
        }

        return entries;
    }

    private static string GetValueTypeName(JsonValueKind kind) => kind switch
    {
        JsonValueKind.String => "String",
        JsonValueKind.Number => "Number",
        JsonValueKind.True or JsonValueKind.False => "Boolean",
        JsonValueKind.Null => "Null",
        _ => "String"
    };
}
