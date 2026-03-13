using System.Net.Http.Json;
using System.Text.Json;
using JZVerse.MicroHuaxia.Dashboard.Abstractions.Configuration;
using JZVerse.MicroHuaxia.Dashboard.Abstractions.Models;
using JZVerse.MicroHuaxia.Dashboard.Abstractions.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Dashboard.Core.Services;

/// <summary>
/// 系统配置聚合服务，从各微服务获取运行时配置
/// </summary>
public class SystemConfigService : ISystemConfigService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DashboardOptions _options;
    private readonly ILogger<SystemConfigService> _logger;

    private static readonly (string ServiceKey, string DisplayName, string ConfigPath)[] ServiceDefinitions =
    [
        ("ServiceDiscovery", "服务发现", "/api/v1/services/config"),
        ("Gateway", "API 网关", "/api/v1/gateway/config"),
        ("MessageQueue", "消息队列", "/api/v1/mq/config"),
        ("Saga", "Saga 编排", "/api/v1/saga/config"),
    ];

    public SystemConfigService(
        IHttpClientFactory httpClientFactory,
        IOptions<DashboardOptions> options,
        ILogger<SystemConfigService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有服务的运行时配置
    /// </summary>
    public async Task<List<ServiceConfigInfo>> GetAllConfigsAsync(CancellationToken cancellationToken = default)
    {
        var tasks = ServiceDefinitions.Select(def =>
            FetchServiceConfigAsync(def.ServiceKey, def.DisplayName, def.ConfigPath, cancellationToken));

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    private async Task<ServiceConfigInfo> FetchServiceConfigAsync(
        string serviceKey, string displayName, string configPath, CancellationToken cancellationToken)
    {
        var info = new ServiceConfigInfo
        {
            ServiceName = serviceKey,
            DisplayName = displayName
        };

        try
        {
            var baseUrl = GetServiceEndpoint(serviceKey);
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(baseUrl);

            var response = await client.GetAsync(configPath, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                info.IsAvailable = false;
                info.ErrorMessage = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
                return info;
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            info.IsAvailable = true;
            info.Entries = FlattenJson(json, "");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取 {ServiceName} 配置失败", displayName);
            info.IsAvailable = false;
            info.ErrorMessage = ex.Message;
        }

        return info;
    }

    private string GetServiceEndpoint(string serviceKey) => serviceKey switch
    {
        "ServiceDiscovery" => _options.ServiceEndpoints.ServiceDiscovery,
        "Gateway" => _options.ServiceEndpoints.Gateway,
        "MessageQueue" => _options.ServiceEndpoints.MessageQueue,
        "Saga" => _options.ServiceEndpoints.Saga,
        _ => _options.ServiceEndpoints.ServiceDiscovery
    };

    /// <summary>
    /// 将 JSON 扁平化为 key-value 列表
    /// </summary>
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
