using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace JZVerse.MicroHuaxia.Gateway.Core.Authentication;

/// <summary>
/// JWT 认证处理器
/// </summary>
public sealed class JwtAuthenticationHandler : IAuthenticationHandler
{
    private readonly ILogger<JwtAuthenticationHandler> _logger;
    private readonly ISecretEncryptor _secretEncryptor;

    public string Name => "jwt";
    public int Priority => 10;

    public JwtAuthenticationHandler(
        ILogger<JwtAuthenticationHandler> logger,
        ISecretEncryptor secretEncryptor)
    {
        _logger = logger;
        _secretEncryptor = secretEncryptor;
    }

    public ValueTask<AuthenticationResult> AuthenticateAsync(
        HttpContext context,
        AuthenticationStrategyConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        // 从 Authorization Header 获取 Token
        var authHeader = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult(AuthenticationResult.Failure("缺少 Bearer Token"));
        }

        var token = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(token))
        {
            return ValueTask.FromResult(AuthenticationResult.Failure("Token 为空"));
        }

        try
        {
            // 解密密钥
            var secretKey = string.IsNullOrEmpty(configuration.SecretKey)
                ? throw new InvalidOperationException("未配置 JWT 密钥")
                : _secretEncryptor.GetOrDecrypt(configuration.SecretKey);

            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = configuration.ValidateIssuer,
                ValidateAudience = configuration.ValidateAudience,
                ValidateLifetime = configuration.ValidateLifetime,
                ValidateIssuerSigningKey = true,
                ValidIssuer = configuration.Issuer,
                ValidAudience = configuration.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ClockSkew = configuration.ClockSkew
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);

            _logger.LogDebug("JWT 认证成功: {UserId}", principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            return ValueTask.FromResult(AuthenticationResult.Success(principal, Name));
        }
        catch (SecurityTokenExpiredException)
        {
            _logger.LogWarning("JWT Token 已过期");
            return ValueTask.FromResult(AuthenticationResult.Failure("Token 已过期"));
        }
        catch (SecurityTokenValidationException ex)
        {
            _logger.LogWarning(ex, "JWT Token 验证失败");
            return ValueTask.FromResult(AuthenticationResult.Failure($"Token 验证失败: {ex.Message}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JWT 认证异常");
            return ValueTask.FromResult(AuthenticationResult.Failure("认证异常"));
        }
    }
}

/// <summary>
/// API Key 认证处理器
/// </summary>
public sealed class ApiKeyAuthenticationHandler : IAuthenticationHandler
{
    private readonly ILogger<ApiKeyAuthenticationHandler> _logger;
    private readonly ISecretEncryptor _secretEncryptor;

    public string Name => "api-key";
    public int Priority => 20;

    public ApiKeyAuthenticationHandler(
        ILogger<ApiKeyAuthenticationHandler> logger,
        ISecretEncryptor secretEncryptor)
    {
        _logger = logger;
        _secretEncryptor = secretEncryptor;
    }

    public ValueTask<AuthenticationResult> AuthenticateAsync(
        HttpContext context,
        AuthenticationStrategyConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        string? apiKey = null;

        // 根据配置的来源获取 API Key
        switch (configuration.ApiKeySource)
        {
            case ApiKeySource.Header:
                apiKey = GetApiKeyFromHeader(context, configuration);
                break;
            case ApiKeySource.Query:
                apiKey = GetApiKeyFromQuery(context, configuration);
                break;
            case ApiKeySource.Both:
                apiKey = GetApiKeyFromHeader(context, configuration)
                         ?? GetApiKeyFromQuery(context, configuration);
                break;
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            return ValueTask.FromResult(AuthenticationResult.Failure("缺少 API Key"));
        }

        // 验证 API Key
        var validKeys = configuration.ValidApiKeys
            .Select(k => _secretEncryptor.GetOrDecrypt(k))
            .ToHashSet();

        if (!validKeys.Contains(apiKey))
        {
            _logger.LogWarning("无效的 API Key");
            return ValueTask.FromResult(AuthenticationResult.Failure("无效的 API Key"));
        }

        // 创建 ClaimsPrincipal
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, $"ApiKey-{apiKey[..Math.Min(8, apiKey.Length)]}"),
            new Claim("auth_method", "api-key")
        };
        var identity = new ClaimsIdentity(claims, "ApiKey");
        var principal = new ClaimsPrincipal(identity);

        _logger.LogDebug("API Key 认证成功");

        return ValueTask.FromResult(AuthenticationResult.Success(principal, Name));
    }

    private static string? GetApiKeyFromHeader(HttpContext context, AuthenticationStrategyConfiguration config)
    {
        var headerName = config.ApiKeyHeaderName ?? "X-API-Key";
        return context.Request.Headers.TryGetValue(headerName, out var value) ? value.ToString() : null;
    }

    private static string? GetApiKeyFromQuery(HttpContext context, AuthenticationStrategyConfiguration config)
    {
        var queryName = config.ApiKeyQueryName ?? "api_key";
        return context.Request.Query.TryGetValue(queryName, out var value) ? value.ToString() : null;
    }
}

/// <summary>
/// Basic 认证处理器
/// </summary>
public sealed class BasicAuthenticationHandler : IAuthenticationHandler
{
    private readonly ILogger<BasicAuthenticationHandler> _logger;
    private readonly ISecretEncryptor _secretEncryptor;

    public string Name => "basic";
    public int Priority => 30;

    public BasicAuthenticationHandler(
        ILogger<BasicAuthenticationHandler> logger,
        ISecretEncryptor secretEncryptor)
    {
        _logger = logger;
        _secretEncryptor = secretEncryptor;
    }

    public ValueTask<AuthenticationResult> AuthenticateAsync(
        HttpContext context,
        AuthenticationStrategyConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var authHeader = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult(AuthenticationResult.Failure("缺少 Basic 认证头"));
        }

        try
        {
            var encodedCredentials = authHeader["Basic ".Length..].Trim();
            var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCredentials));
            var parts = credentials.Split(':', 2);

            if (parts.Length != 2)
            {
                return ValueTask.FromResult(AuthenticationResult.Failure("无效的认证格式"));
            }

            var username = parts[0];
            var password = parts[1];

            var expectedUsername = configuration.BasicUsername ?? "";
            var expectedPassword = string.IsNullOrEmpty(configuration.BasicPassword)
                ? ""
                : _secretEncryptor.GetOrDecrypt(configuration.BasicPassword);

            if (username != expectedUsername || password != expectedPassword)
            {
                _logger.LogWarning("Basic 认证失败: 用户名或密码错误");
                return ValueTask.FromResult(AuthenticationResult.Failure("用户名或密码错误"));
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, username),
                new Claim("auth_method", "basic")
            };
            var identity = new ClaimsIdentity(claims, "Basic");
            var principal = new ClaimsPrincipal(identity);

            _logger.LogDebug("Basic 认证成功: {Username}", username);

            return ValueTask.FromResult(AuthenticationResult.Success(principal, Name));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Basic 认证异常");
            return ValueTask.FromResult(AuthenticationResult.Failure("认证异常"));
        }
    }
}
