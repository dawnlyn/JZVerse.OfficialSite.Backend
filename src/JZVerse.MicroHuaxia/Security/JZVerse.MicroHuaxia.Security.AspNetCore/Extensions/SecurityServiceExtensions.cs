using JZVerse.MicroHuaxia.Security.Audit;
using JZVerse.MicroHuaxia.Security.Authentication;
using JZVerse.MicroHuaxia.Security.Authorization;
using JZVerse.MicroHuaxia.Security.Cryptography;
using JZVerse.MicroHuaxia.Security.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace JZVerse.MicroHuaxia.Security;

/// <summary>
/// 安全服务扩展方法
/// </summary>
public static class SecurityServiceExtensions
{
    /// <summary>
    /// 添加安全平台核心服务
    /// </summary>
    public static IServiceCollection AddSecurityCore(this IServiceCollection services)
    {
        // 注册国密算法提供者
        services.AddSingleton<ISm2Provider, Sm2Provider>();
        services.AddSingleton<ISm3Provider, Sm3Provider>();
        services.AddSingleton<ISm4Provider, Sm4Provider>();

        // 注册存储实现（使用内存实现，生产环境应替换为数据库存储）
        services.AddSingleton<IPolicyStore, InMemoryPolicyStore>();
        services.AddSingleton<IAuditStore, InMemoryAuditStore>();

        // 注册核心服务
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IAuthorizationEngine, PolicyEngine>();
        services.AddSingleton<IAuditLogger, AuditLogger>();

        return services;
    }

    /// <summary>
    /// 添加安全平台认证服务
    /// </summary>
    public static IServiceCollection AddSecurityAuthentication(this IServiceCollection services)
    {
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        return services;
    }

    /// <summary>
    /// 添加安全平台授权服务
    /// </summary>
    public static IServiceCollection AddSecurityAuthorization(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationService, AuthorizationService>();
        return services;
    }

    /// <summary>
    /// 添加完整的安全平台服务
    /// </summary>
    public static IServiceCollection AddSecurityPlatform(this IServiceCollection services)
    {
        services.AddSecurityCore();
        services.AddSecurityAuthentication();
        services.AddSecurityAuthorization();

        // 注册 HTTP 上下文访问器
        services.AddHttpContextAccessor();

        return services;
    }
}

/// <summary>
/// 认证服务实现
/// </summary>
internal sealed class AuthenticationService : IAuthenticationService
{
    private readonly ITokenService _tokenService;
    private readonly ISm3Provider _sm3;

    public AuthenticationService(ITokenService tokenService, ISm3Provider sm3)
    {
        _tokenService = tokenService;
        _sm3 = sm3;
    }

    public Task<AuthenticationResult> AuthenticateWithMutualTlsAsync(byte[] clientCertificate, CancellationToken cancellationToken = default)
    {
        // mTLS 认证实现（简化）
        throw new NotImplementedException("mTLS 认证需要证书解析实现");
    }

    public Task<AuthenticationResult> AuthenticateWithTokenAsync(string token, TokenType tokenType = TokenType.AccessToken, CancellationToken cancellationToken = default)
    {
        var result = _tokenService.ValidateToken(token, tokenType);
        if (result.IsValid)
        {
            return Task.FromResult(AuthenticationResult.Success(result.Identity!, AuthenticationMethod.JwtToken));
        }
        return Task.FromResult(AuthenticationResult.Failed(result.ErrorMessage ?? "令牌验证失败", AuthenticationMethod.JwtToken));
    }

    public Task<AuthenticationResult> AuthenticateWithApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        // API 密钥认证实现（简化）
        throw new NotImplementedException("API 密钥认证需要数据库查询实现");
    }

    public Task<AuthenticationResult> AuthenticateWithPasswordAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        // 用户名密码认证实现（简化）
        throw new NotImplementedException("密码认证需要用户数据库实现");
    }

    public Task<TokenInfo> GenerateServiceTokenAsync(SecurityIdentity identity, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_tokenService.GenerateToken(identity, TokenType.ServiceToken));
    }

    public Task<TokenInfo> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_tokenService.RefreshToken(refreshToken));
    }

    public Task<bool> RevokeTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        // 解析令牌获取 TokenId
        var result = _tokenService.ValidateToken(token, TokenType.AccessToken);
        if (result.IsValid)
        {
            _tokenService.RevokeToken(result.Identity!.IdentityId);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
}

/// <summary>
/// 授权服务实现
/// </summary>
internal sealed class AuthorizationService : IAuthorizationService
{
    private readonly IAuthorizationEngine _engine;

    public AuthorizationService(IAuthorizationEngine engine)
    {
        _engine = engine;
    }

    public Task<AccessDecision> AuthorizeAsync(AccessRequest request, CancellationToken cancellationToken = default)
    {
        return _engine.EvaluateAsync(request, cancellationToken);
    }
}

/// <summary>
/// 授权服务接口
/// </summary>
public interface IAuthorizationService
{
    /// <summary>
    /// 授权访问
    /// </summary>
    Task<AccessDecision> AuthorizeAsync(AccessRequest request, CancellationToken cancellationToken = default);
}
