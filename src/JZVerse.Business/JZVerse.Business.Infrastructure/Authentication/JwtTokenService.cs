using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using JZVerse.Business.Abstractions.Authentication;
using JZVerse.Business.Abstractions.Encryption;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

namespace JZVerse.Business.Infrastructure.Authentication;

/// <summary>
/// JWT Token 服务实现
/// </summary>
public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly ISm2Encryptor? _sm2Encryptor;
    private readonly IConnectionMultiplexer? _redis;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    private byte[]? _secretKeyBytes;

    public JwtTokenService(
        IOptions<JwtOptions> options,
        ISm2Encryptor? sm2Encryptor = null,
        IConnectionMultiplexer? redis = null)
    {
        _options = options.Value;
        _sm2Encryptor = sm2Encryptor;
        _redis = redis;
    }

    /// <inheritdoc />
    public Task<string> GenerateAccessTokenAsync(
        Guid userId,
        string username,
        IEnumerable<string> roles,
        IDictionary<string, string>? additionalClaims = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        if (additionalClaims is not null)
        {
            foreach (var (key, value) in additionalClaims)
            {
                claims.Add(new Claim(key, value));
            }
        }

        var secretKey = GetSecretKey();
        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(secretKey),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.ExpirationMinutes),
            signingCredentials: signingCredentials);

        var tokenString = _tokenHandler.WriteToken(token);

        // 如果启用 Payload 加密
        if (_options.EnablePayloadEncryption && _sm2Encryptor is not null)
        {
            tokenString = _sm2Encryptor.Encrypt(tokenString);
        }

        return Task.FromResult(tokenString);
    }

    /// <inheritdoc />
    public async Task<string> GenerateRefreshTokenAsync(Guid userId)
    {
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var tokenHash = ComputeHash(refreshToken);

        // 存储到 Redis
        if (_redis is not null)
        {
            var db = _redis.GetDatabase();
            var key = GetRefreshTokenKey(userId);
            var expiry = TimeSpan.FromDays(_options.RefreshTokenExpirationDays);
            await db.StringSetAsync(key, tokenHash, expiry);
        }

        return refreshToken;
    }

    /// <inheritdoc />
    public Task<ClaimsPrincipal?> ValidateAccessTokenAsync(string token)
    {
        try
        {
            // 如果启用 Payload 加密，先解密
            if (_options.EnablePayloadEncryption && _sm2Encryptor is not null && _sm2Encryptor.IsEncrypted(token))
            {
                token = _sm2Encryptor.Decrypt(token);
            }

            var secretKey = GetSecretKey();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _options.Issuer,
                ValidAudience = _options.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(secretKey),
                ClockSkew = TimeSpan.FromSeconds(_options.ClockSkewSeconds)
            };

            var principal = _tokenHandler.ValidateToken(token, validationParameters, out _);
            return Task.FromResult<ClaimsPrincipal?>(principal);
        }
        catch
        {
            return Task.FromResult<ClaimsPrincipal?>(null);
        }
    }

    /// <inheritdoc />
    public async Task<Guid?> ValidateRefreshTokenAsync(string refreshToken)
    {
        if (_redis is null)
            return null;

        var tokenHash = ComputeHash(refreshToken);
        var db = _redis.GetDatabase();

        // 遍历查找匹配的用户（实际应用中应该有更好的索引方式）
        // 这里简化处理，假设 refreshToken 中包含了 userId 信息
        // 或者使用单独的映射表

        // 简化实现：尝试从 token 中提取信息
        // 实际应用中应该使用独立的 refresh token 存储结构
        return null;
    }

    /// <inheritdoc />
    public async Task RevokeRefreshTokenAsync(Guid userId)
    {
        if (_redis is not null)
        {
            var db = _redis.GetDatabase();
            var key = GetRefreshTokenKey(userId);
            await db.KeyDeleteAsync(key);
        }
    }

    private byte[] GetSecretKey()
    {
        if (_secretKeyBytes is not null)
            return _secretKeyBytes;

        var secretKeyString = _options.SecretKey;

        // 如果启用 SM2 加密 SecretKey，先解密
        if (_options.EnableSm2Encryption && _sm2Encryptor is not null)
        {
            secretKeyString = _sm2Encryptor.GetOrDecrypt(secretKeyString);
        }

        _secretKeyBytes = Encoding.UTF8.GetBytes(secretKeyString);
        return _secretKeyBytes;
    }

    private static string GetRefreshTokenKey(Guid userId) => $"refresh_token:{userId}";

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes);
    }
}
