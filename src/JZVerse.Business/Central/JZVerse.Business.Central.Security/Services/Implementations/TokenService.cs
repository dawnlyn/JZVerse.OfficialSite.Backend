using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JZVerse.Business.Central.Security.Database;
using JZVerse.Business.Central.Security.Database.Models;
using JZVerse.Business.Central.Security.Services.Interfaces;
using JZVerse.MicroHuaxia.DataAccess.Abstractions;
using Microsoft.Extensions.Logging;
using MicrosoftIdentityModel = Microsoft.IdentityModel.Tokens;

namespace JZVerse.Business.Central.Security.Services.Implementations;

/// <summary>
/// Token服务实现
/// </summary>
public sealed class TokenService : ITokenService
{
    private readonly IDbExecutor _db;
    private readonly ILogger<TokenService> _logger;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    // JWT配置（实际应从配置中读取）
    private const string SecretKey = "JZVerse-Security-Center-Secret-Key-2024";
    private const string Issuer = "JZVerse.Security";
    private const string Audience = "JZVerse.OfficialSite";
    private const int AccessTokenExpirationMinutes = 60;
    private const int RefreshTokenExpirationDays = 7;

    public TokenService(IDbExecutor db, ILogger<TokenService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TokenResult> GenerateTokensAsync(
        Guid userId,
        string username,
        Dictionary<string, string>? claims = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        try
        {
            var now = DateTime.UtcNow;
            var expiresAt = now.AddMinutes(AccessTokenExpirationMinutes);
            var refreshTokenExpiresAt = now.AddDays(RefreshTokenExpirationDays);

            // 生成Access Token
            var accessToken = GenerateAccessToken(userId, username, claims, now, expiresAt);

            // 生成Refresh Token
            var refreshToken = GenerateRefreshToken();

            // 保存到数据库
            var tokenRecord = new SecurityToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Token = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = expiresAt,
                RefreshTokenExpiresAt = refreshTokenExpiresAt,
                IsRevoked = false,
                CreatedAt = now,
                CreatedIp = ipAddress,
                UserAgent = userAgent
            };

            await _db.ExecuteAsync(SecuritySql.InsertToken, tokenRecord);

            _logger.LogInformation("Token生成成功 - UserId: {UserId}, TokenId: {TokenId}", userId, tokenRecord.Id);

            return TokenResult.SuccessResult(
                accessToken,
                refreshToken,
                (int)(expiresAt - now).TotalSeconds,
                expiresAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token生成失败 - UserId: {UserId}", userId);
            return TokenResult.FailureResult($"Token生成失败: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<TokenValidationResult> ValidateTokenAsync(string token)
    {
        try
        {
            var secretKey = new MicrosoftIdentityModel.SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
            var validationParameters = new MicrosoftIdentityModel.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = Issuer,
                ValidAudience = Audience,
                IssuerSigningKey = secretKey,
                ClockSkew = TimeSpan.FromMinutes(5)
            };

            var principal = _tokenHandler.ValidateToken(token, validationParameters, out _);
            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var usernameClaim = principal.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || string.IsNullOrEmpty(usernameClaim))
            {
                return TokenValidationResult.Invalid("Token中缺少用户信息");
            }

            // 检查数据库中是否被撤销
            var dbToken = await _db.QueryFirstOrDefaultAsync<SecurityToken>(
                SecuritySql.GetTokenByValue,
                new { Token = token });

            if (dbToken is null)
            {
                return TokenValidationResult.Invalid("Token不存在");
            }

            if (dbToken.IsRevoked)
            {
                return TokenValidationResult.Invalid("Token已被撤销");
            }

            if (dbToken.ExpiresAt < DateTime.UtcNow)
            {
                return TokenValidationResult.Invalid("Token已过期");
            }

            // 提取额外声明
            var claims = new Dictionary<string, string>();
            foreach (var claim in principal.Claims)
            {
                if (claim.Type != ClaimTypes.NameIdentifier && claim.Type != ClaimTypes.Name)
                {
                    claims[claim.Type] = claim.Value;
                }
            }

            return TokenValidationResult.Valid(Guid.Parse(userIdClaim), usernameClaim, claims);
        }
        catch (MicrosoftIdentityModel.SecurityTokenExpiredException)
        {
            return TokenValidationResult.Invalid("Token已过期");
        }
        catch (MicrosoftIdentityModel.SecurityTokenInvalidSignatureException)
        {
            return TokenValidationResult.Invalid("Token签名无效");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token验证失败");
            return TokenValidationResult.Invalid($"Token验证失败: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<TokenResult> RefreshTokensAsync(string refreshToken)
    {
        try
        {
            // 查找Refresh Token对应的记录
            var tokenRecord = await _db.QueryFirstOrDefaultAsync<SecurityToken>(
                SecuritySql.GetTokenByRefreshToken,
                new { RefreshToken = refreshToken });

            if (tokenRecord is null)
            {
                return TokenResult.FailureResult("无效的Refresh Token");
            }

            if (tokenRecord.IsRevoked)
            {
                return TokenResult.FailureResult("Token已被撤销");
            }

            if (tokenRecord.RefreshTokenExpiresAt < DateTime.UtcNow)
            {
                return TokenResult.FailureResult("Refresh Token已过期，请重新登录");
            }

            // 撤销旧的Token
            await RevokeTokenAsync(tokenRecord.Token);

            // 生成新的Token对
            var claims = string.IsNullOrEmpty(tokenRecord.UserAgent)
                ? null
                : new Dictionary<string, string> { { "UserAgent", tokenRecord.UserAgent } };

            return await GenerateTokensAsync(
                tokenRecord.UserId,
                "", // 实际应从用户服务获取
                claims,
                tokenRecord.CreatedIp,
                tokenRecord.UserAgent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token刷新失败");
            return TokenResult.FailureResult($"Token刷新失败: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<bool> RevokeTokenAsync(string token)
    {
        try
        {
            var affected = await _db.ExecuteAsync(
                SecuritySql.RevokeToken,
                new { Token = token, RevokedAt = DateTime.UtcNow });

            if (affected > 0)
            {
                _logger.LogInformation("Token已撤销");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "撤销Token失败");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> RevokeAllUserTokensAsync(Guid userId)
    {
        try
        {
            // 使用SQL语句撤销用户所有Token
            const string sql = @"
                UPDATE security_tokens 
                SET is_revoked = TRUE, revoked_at = @RevokedAt 
                WHERE user_id = @UserId AND is_revoked = FALSE";

            var affected = await _db.ExecuteAsync(sql, new { UserId = userId, RevokedAt = DateTime.UtcNow });

            _logger.LogInformation("已撤销用户所有Token - UserId: {UserId}, Count: {Count}", userId, affected);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "撤销用户所有Token失败 - UserId: {UserId}", userId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<int> CleanExpiredTokensAsync()
    {
        try
        {
            var now = DateTime.UtcNow;
            var revokedBefore = now.AddDays(-7); // 保留已撤销Token 7天

            var affected = await _db.ExecuteAsync(
                SecuritySql.DeleteExpiredTokens,
                new { Now = now, RevokedBefore = revokedBefore });

            _logger.LogInformation("清理过期Token完成 - 删除数量: {Count}", affected);
            return affected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理过期Token失败");
            return 0;
        }
    }

    private string GenerateAccessToken(
        Guid userId,
        string username,
        Dictionary<string, string>? claims,
        DateTime notBefore,
        DateTime expires)
    {
        var securityKey = new MicrosoftIdentityModel.SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
        var credentials = new MicrosoftIdentityModel.SigningCredentials(securityKey, MicrosoftIdentityModel.SecurityAlgorithms.HmacSha256);

        var tokenClaims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(notBefore).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        // 添加额外声明
        if (claims != null)
        {
            foreach (var (key, value) in claims)
            {
                tokenClaims.Add(new Claim(key, value));
            }
        }

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: tokenClaims,
            notBefore: notBefore,
            expires: expires,
            signingCredentials: credentials);

        return _tokenHandler.WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }
        return Convert.ToBase64String(randomBytes);
    }

    /// <inheritdoc />
    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            // 尝试查询一条记录验证数据库连接
            const string sql = "SELECT COUNT(*) FROM security_tokens LIMIT 1";
            await _db.ExecuteScalarAsync<int>(sql);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TokenService健康检查失败");
            return false;
        }
    }
}
