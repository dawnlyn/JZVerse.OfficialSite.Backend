using System.Net.Http.Headers;
using System.Net.Http.Json;
using JZVerse.MicroHuaxia.Security.Authentication;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Security.Http;

/// <summary>
/// 安全 HTTP 客户端
/// </summary>
public sealed class SecureHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly TokenProvider _tokenProvider;
    private readonly ILogger<SecureHttpClient> _logger;

    public SecureHttpClient(
        HttpClient httpClient,
        TokenProvider tokenProvider,
        ILogger<SecureHttpClient> logger)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    /// <summary>
    /// 发送 GET 请求
    /// </summary>
    public async Task<T?> GetAsync<T>(string url, CancellationToken cancellationToken = default)
    {
        await AddAuthorizationHeaderAsync();
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }

    /// <summary>
    /// 发送 POST 请求
    /// </summary>
    public async Task<T?> PostAsync<T>(string url, object data, CancellationToken cancellationToken = default)
    {
        await AddAuthorizationHeaderAsync();
        var response = await _httpClient.PostAsJsonAsync(url, data, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }

    /// <summary>
    /// 添加授权请求头
    /// </summary>
    private async Task AddAuthorizationHeaderAsync()
    {
        var token = await _tokenProvider.GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", token);
        }
    }
}

/// <summary>
/// 令牌提供者
/// </summary>
public class TokenProvider
{
    private readonly string _serverAddress;
    private readonly string _serviceIdentity;
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiresAt;

    public TokenProvider(string serverAddress, string serviceIdentity)
    {
        _serverAddress = serverAddress;
        _serviceIdentity = serviceIdentity;
    }

    /// <summary>
    /// 获取有效令牌
    /// </summary>
    public async Task<string?> GetTokenAsync()
    {
        // 检查令牌是否过期
        if (_cachedToken != null && DateTimeOffset.UtcNow < _tokenExpiresAt.AddMinutes(-5))
        {
            return _cachedToken;
        }

        // 从安全平台获取新令牌
        await RefreshTokenAsync();
        return _cachedToken;
    }

    /// <summary>
    /// 刷新令牌
    /// </summary>
    private async Task RefreshTokenAsync()
    {
        using var client = new HttpClient();
        client.BaseAddress = new Uri(_serverAddress);

        // 创建服务身份
        var identity = new SecurityIdentity
        {
            IdentityId = _serviceIdentity,
            Type = IdentityType.Service,
            ServiceName = _serviceIdentity,
            IssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        var request = new { Identity = identity };
        var response = await client.PostAsJsonAsync("api/v1/authentication/service-token", request);

        if (response.IsSuccessStatusCode)
        {
            var tokenInfo = await response.Content.ReadFromJsonAsync<TokenInfo>();
            if (tokenInfo != null)
            {
                _cachedToken = tokenInfo.AccessToken;
                _tokenExpiresAt = tokenInfo.ExpiresAt;
            }
        }
    }
}
