using System.Net.Http.Json;
using JZVerse.MicroHuaxia.Security.Configuration;
using JZVerse.MicroHuaxia.Security.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Security.Authorization;

/// <summary>
/// 授权客户端
/// </summary>
public sealed class AuthorizationClient
{
    private readonly HttpClient _httpClient;
    private readonly TokenProvider _tokenProvider;
    private readonly ILogger<AuthorizationClient> _logger;

    public AuthorizationClient(
        IOptions<SecurityClientOptions> options,
        ILogger<AuthorizationClient> logger)
    {
        _logger = logger;
        var opt = options.Value;
        
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(opt.ServerAddress),
            Timeout = TimeSpan.FromSeconds(opt.RequestTimeoutSeconds)
        };
        
        _tokenProvider = new TokenProvider(opt.ServerAddress, opt.ServiceIdentity);
    }

    /// <summary>
    /// 评估访问请求
    /// </summary>
    public async Task<AccessDecision> EvaluateAsync(AccessRequest request)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            
            var response = await _httpClient.PostAsJsonAsync("api/v1/authorization/evaluate", new
            {
                subject = request.Subject,
                resource = request.Resource,
                action = request.Action,
                request.Namespace,
                context = request.Context
            });

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AccessDecision>();
                return result ?? AccessDecision.DefaultDeny;
            }

            _logger.LogWarning(
                "授权评估请求失败: {StatusCode}", 
                response.StatusCode);
            return AccessDecision.DefaultDeny;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "授权评估请求异常");
            return AccessDecision.DefaultDeny;
        }
    }

    /// <summary>
    /// 检查是否允许访问
    /// </summary>
    public async Task<bool> IsAllowedAsync(
        SecurityIdentity subject,
        string resource,
        string action)
    {
        var request = AccessRequest.Create(subject, resource, action);
        var decision = await EvaluateAsync(request);
        return decision.Allowed;
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
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
    }
}
