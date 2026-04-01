using System.Security.Claims;
using System.Text.Encodings.Web;
using JZVerse.Business.Central.Security.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace JZVerse.Business.Central.Security.Middleware;

/// <summary>
/// 安全认证中间件 - 基于Token的身份验证
/// </summary>
public class SecurityAuthenticationHandler : AuthenticationHandler<SecurityAuthenticationOptions>
{
    private readonly ITokenService _tokenService;
    private readonly ILogger<SecurityAuthenticationHandler> _logger;

    public SecurityAuthenticationHandler(
        IOptionsMonitor<SecurityAuthenticationOptions> options,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder,
        ITokenService tokenService)
        : base(options, loggerFactory, encoder)
    {
        _tokenService = tokenService;
        _logger = loggerFactory.CreateLogger<SecurityAuthenticationHandler>();
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // 检查是否启用匿名访问
        if (Options.AnonymousPaths.Any(path => Request.Path.StartsWithSegments(path)))
        {
            return AuthenticateResult.NoResult();
        }

        // 获取Token
        var token = GetTokenFromRequest();
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Authentication failed: No token found in request");
            return AuthenticateResult.Fail("No token found in request");
        }

        // 验证Token
        var validationResult = await _tokenService.ValidateTokenAsync(token);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("Authentication failed: {Error}", validationResult.ErrorMessage);
            return AuthenticateResult.Fail(validationResult.ErrorMessage ?? "Invalid token");
        }

        // 创建ClaimsIdentity
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, validationResult.UserId?.ToString() ?? string.Empty),
            new Claim(ClaimTypes.Name, validationResult.Username ?? string.Empty)
        };

        // 添加额外声明
        if (validationResult.Claims != null)
        {
            foreach (var claim in validationResult.Claims)
            {
                claims.Add(new Claim(claim.Key, claim.Value));
            }
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        _logger.LogDebug("Authentication successful for user {UserId}", validationResult.UserId);
        return AuthenticateResult.Success(ticket);
    }

    private string? GetTokenFromRequest()
    {
        // 从Authorization头获取
        var authorization = Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authorization) && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authorization.Substring("Bearer ".Length).Trim();
        }

        // 从查询参数获取
        if (Request.Query.TryGetValue("access_token", out var queryToken))
        {
            return queryToken.FirstOrDefault();
        }

        // 从Cookie获取
        if (Request.Cookies.TryGetValue("access_token", out var cookieToken))
        {
            return cookieToken;
        }

        return null;
    }
}

/// <summary>
/// 安全认证选项
/// </summary>
public class SecurityAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// 匿名访问路径列表
    /// </summary>
    public List<string> AnonymousPaths { get; set; } = new()
    {
        "/api/v1/auth/login",
        "/api/v1/auth/refresh",
        "/health",
        "/swagger"
    };

    /// <summary>
    /// Token验证失败时的处理方式
    /// </summary>
    public bool Challenge { get; set; } = true;
}
