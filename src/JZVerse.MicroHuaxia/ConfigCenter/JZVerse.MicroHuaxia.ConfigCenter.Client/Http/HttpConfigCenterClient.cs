using System.Net.Http.Json;
using System.Text.Json;
using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;
using JZVerse.MicroHuaxia.ConfigCenter.Client.Cache;
using JZVerse.MicroHuaxia.ConfigCenter.Client.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.ConfigCenter.Client.Http;

/// <summary>
/// HTTP 配置中心客户端实现
/// </summary>
public class HttpConfigCenterClient : IConfigCenterClient
{
    private readonly HttpClient _httpClient;
    private readonly ConfigCenterClientOptions _options;
    private readonly LocalConfigCache _cache;
    private readonly ILogger<HttpConfigCenterClient> _logger;
    private readonly List<string> _serverUrls;
    private int _currentServerIndex;
    private readonly Lock _serverLock = new();

    private readonly Dictionary<string, Func<ConfigChangeEvent, Task>> _subscriptions = new();

    public HttpConfigCenterClient(
        HttpClient httpClient,
        IOptions<ConfigCenterClientOptions> options,
        ILogger<HttpConfigCenterClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _serverUrls = [.. _options.ServerUrls];
        _cache = new LocalConfigCache(_options.LocalCachePath);

        // 初始化客户端 ID
        if (string.IsNullOrEmpty(_options.ClientId))
        {
            _options.ClientId = Guid.NewGuid().ToString("N");
        }
    }

    public async Task<Dictionary<string, string>> GetConfigAsync(
        string namespaceId,
        string environmentId,
        CancellationToken cancellationToken = default)
    {
        // 尝试从缓存获取
        var cached = _cache.Get(namespaceId, environmentId);

        try
        {
            var config = await ExecuteWithFailoverAsync(
                async baseUrl =>
                {
                    var url = BuildUrl(baseUrl,
                        $"/api/v1/discovery/applications/{_options.ApplicationId}/environments/{environmentId}/namespaces/{namespaceId}",
                        new Dictionary<string, string?>
                        {
                            ["clientId"] = _options.ClientId,
                            ["tags"] = string.Join(",", _options.Tags),
                        });

                    var response = await _httpClient.GetAsync(url, cancellationToken);
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(cancellationToken)
                        ?? new Dictionary<string, string>();
                },
                cancellationToken);

            // 更新缓存
            _cache.Set(namespaceId, environmentId, config, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get config from server, using cached version");

            // 返回缓存的配置（容灾）
            if (cached is not null)
            {
                return cached;
            }

            throw;
        }
    }

    public async Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
    {
        // 从所有已加载的命名空间中查找
        foreach (var namespaceId in _options.Namespaces)
        {
            var config = await GetConfigAsync(namespaceId, _options.EnvironmentId, cancellationToken);
            if (config.TryGetValue(key, out var value))
            {
                return value;
            }
        }
        return null;
    }

    public async Task<T?> GetValueAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var value = await GetValueAsync(key, cancellationToken);
        if (value is null)
            return default;

        return JsonSerializer.Deserialize<T>(value);
    }

    public Task SubscribeAsync(
        string namespaceId,
        Func<ConfigChangeEvent, Task> callback,
        CancellationToken cancellationToken = default)
    {
        _subscriptions[namespaceId] = callback;
        _logger.LogInformation("Subscribed to config changes for namespace {NamespaceId}", namespaceId);
        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(string namespaceId, CancellationToken cancellationToken = default)
    {
        _subscriptions.Remove(namespaceId);
        _logger.LogInformation("Unsubscribed from config changes for namespace {NamespaceId}", namespaceId);
        return Task.CompletedTask;
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Refreshing all config namespaces");

        foreach (var namespaceId in _options.Namespaces)
        {
            try
            {
                await GetConfigAsync(namespaceId, _options.EnvironmentId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh config for namespace {NamespaceId}", namespaceId);
            }
        }
    }

    private async Task<T> ExecuteWithFailoverAsync<T>(
        Func<string, Task<T>> action,
        CancellationToken cancellationToken)
    {
        var attempts = 0;
        var maxAttempts = _serverUrls.Count * _options.RetryCount;

        while (attempts < maxAttempts)
        {
            var serverUrl = GetCurrentServerUrl();

            try
            {
                return await action(serverUrl);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "Request to {ServerUrl} failed, attempting failover", serverUrl);
                SwitchToNextServer();
                attempts++;
            }
        }

        throw new InvalidOperationException("All servers are unavailable");
    }

    private string GetCurrentServerUrl()
    {
        lock (_serverLock)
        {
            return _serverUrls[_currentServerIndex];
        }
    }

    private void SwitchToNextServer()
    {
        lock (_serverLock)
        {
            _currentServerIndex = (_currentServerIndex + 1) % _serverUrls.Count;
            _logger.LogInformation("Switched to server {ServerUrl}", _serverUrls[_currentServerIndex]);
        }
    }

    private static string BuildUrl(string baseUrl, string path, Dictionary<string, string?>? queryParams = null)
    {
        var url = $"{baseUrl.TrimEnd('/')}{path}";

        if (queryParams is { Count: > 0 })
        {
            var queryString = string.Join("&",
                queryParams
                    .Where(kv => !string.IsNullOrEmpty(kv.Value))
                    .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));

            if (!string.IsNullOrEmpty(queryString))
            {
                url += $"?{queryString}";
            }
        }

        return url;
    }
}
