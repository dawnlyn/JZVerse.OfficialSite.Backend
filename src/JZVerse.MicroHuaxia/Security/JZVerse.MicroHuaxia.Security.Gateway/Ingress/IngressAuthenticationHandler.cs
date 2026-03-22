using System.Security.Cryptography.X509Certificates;
using JZVerse.MicroHuaxia.Security.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Security.Gateway.Ingress;

/// <summary>
/// 入口网关认证处理器
/// </summary>
public sealed class IngressAuthenticationHandler
{
    private readonly IAuthenticationService _authService;
    private readonly ILogger<IngressAuthenticationHandler> _logger;

    public IngressAuthenticationHandler(
        IAuthenticationService authService,
        ILogger<IngressAuthenticationHandler> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// 处理请求认证
    /// </summary>
    public async Task<AuthenticationResult> AuthenticateAsync(HttpContext context)
    {
        // 1. 检查 mTLS 证书
        var clientCert = await GetClientCertificateAsync(context);
        if (clientCert != null)
        {
            var mtlsResult = await AuthenticateWithCertificateAsync(clientCert);
            if (mtlsResult.Succeeded)
            {
                _logger.LogDebug("mTLS 认证成功: {Identity}", mtlsResult.Identity?.IdentityId);
                return mtlsResult;
            }
        }

        // 2. 检查 Authorization 请求头
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader))
        {
            var tokenResult = await AuthenticateWithTokenAsync(authHeader);
            if (tokenResult.Succeeded)
            {
                _logger.LogDebug("令牌认证成功: {Identity}", tokenResult.Identity?.IdentityId);
                return tokenResult;
            }
        }

        // 3. 检查 API Key
        var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
        if (!string.IsNullOrEmpty(apiKey))
        {
            var apiKeyResult = await _authService.AuthenticateWithApiKeyAsync(apiKey);
            if (apiKeyResult.Succeeded)
            {
                _logger.LogDebug("API Key 认证成功: {Identity}", apiKeyResult.Identity?.IdentityId);
                return apiKeyResult;
            }
        }

        // 认证失败
        return AuthenticationResult.Failed("未提供有效的认证凭证");
    }

    /// <summary>
    /// 转换外部身份为内部身份
    /// </summary>
    public SecurityIdentity TransformToInternalIdentity(SecurityIdentity externalIdentity)
    {
        // 添加内部标识
        var claims = new Dictionary<string, string>(externalIdentity.Claims)
        {
            ["x-request-origin"] = "external",
            ["x-auth-method"] = externalIdentity.AuthenticationMethod.ToString()
        };

        return externalIdentity with
        {
            Claims = claims,
            Type = externalIdentity.Type == IdentityType.ExternalClient 
                ? IdentityType.Service 
                : externalIdentity.Type
        };
    }

    /// <summary>
    /// 获取客户端证书
    /// </summary>
    private Task<X509Certificate2?> GetClientCertificateAsync(HttpContext context)
    {
        var cert = context.Connection.ClientCertificate;
        return Task.FromResult(cert);
    }

    /// <summary>
    /// 使用证书认证
    /// </summary>
    private Task<AuthenticationResult> AuthenticateWithCertificateAsync(X509Certificate2 certificate)
    {
        // 提取证书中的 SPIFFE ID
        var spiffeId = ExtractSpiffeId(certificate);
        if (!string.IsNullOrEmpty(spiffeId))
        {
            var identity = ParseSpiffeId(spiffeId);
            identity = identity with { AuthenticationMethod = AuthenticationMethod.MutualTls };
            return Task.FromResult(AuthenticationResult.Success(identity, AuthenticationMethod.MutualTls));
        }

        return Task.FromResult(AuthenticationResult.Failed("无效的客户端证书"));
    }

    /// <summary>
    /// 使用令牌认证
    /// </summary>
    private Task<AuthenticationResult> AuthenticateWithTokenAsync(string authHeader)
    {
        // 解析 Bearer 令牌
        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader.Substring(7);
            return _authService.AuthenticateWithTokenAsync(token);
        }

        return Task.FromResult(AuthenticationResult.Failed("不支持的认证方案"));
    }

    /// <summary>
    /// 从证书提取 SPIFFE ID
    /// </summary>
    private string? ExtractSpiffeId(X509Certificate2 certificate)
    {
        // 从证书的 Subject Alternative Name 或 Subject 中提取 SPIFFE ID
        // 简化实现
        var subject = certificate.Subject;
        if (subject.Contains("spiffe://"))
        {
            var start = subject.IndexOf("spiffe://");
            var end = subject.IndexOf(",", start);
            if (end == -1) end = subject.Length;
            return subject[start..end].Trim();
        }
        return null;
    }

    /// <summary>
    /// 解析 SPIFFE ID
    /// </summary>
    private SecurityIdentity ParseSpiffeId(string spiffeId)
    {
        // 解析 spiffe://trust-domain/ns/namespace/sa/service-account
        var parts = spiffeId.Split('/');
        var identity = new SecurityIdentity
        {
            IdentityId = spiffeId,
            Type = IdentityType.Service,
            TrustDomain = parts.Length > 2 ? parts[2] : "cluster.local"
        };

        for (int i = 3; i < parts.Length - 1; i += 2)
        {
            if (i + 1 < parts.Length)
            {
                switch (parts[i])
                {
                    case "ns":
                        identity = identity with { Namespace = parts[i + 1] };
                        break;
                    case "sa":
                        identity = identity with { ServiceName = parts[i + 1] };
                        break;
                }
            }
        }

        return identity;
    }
}
