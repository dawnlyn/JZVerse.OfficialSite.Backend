using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JZVerse.MicroHuaxia.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace JZVerse.MicroHuaxia.Security.Authentication;

/// <summary>
/// 令牌服务实现
/// </summary>
public sealed class TokenService : ITokenService
{
    private readonly ISm4Provider _sm4;
    private readonly ISm3Provider _sm3;
    private readonly ILogger<TokenService> _logger;
    private readonly byte[] _masterKey;

    // 令牌缓存（实际生产环境应使用分布式缓存）
    private readonly Dictionary<string, TokenEntry> _tokenCache = new();
    private readonly ReaderWriterLockSlim _cacheLock = new();

    public TokenService(
        ISm4Provider sm4,
        ISm3Provider sm3,
        ILogger<TokenService> logger)
    {
        _sm4 = sm4;
        _sm3 = sm3;
        _logger = logger;
        _masterKey = InitializeMasterKey();
    }

    /// <inheritdoc />
    public TokenInfo GenerateToken(SecurityIdentity identity, TokenType tokenType, TimeSpan? lifetime = null)
    {
        // 确定令牌有效期
        TimeSpan effectiveLifetime = tokenType switch
        {
            TokenType.AccessToken => lifetime ?? TimeSpan.FromHours(1),
            TokenType.RefreshToken => lifetime ?? TimeSpan.FromDays(7),
            TokenType.ServiceToken => lifetime ?? TimeSpan.FromHours(1),
            _ => TimeSpan.FromHours(1)
        };

        var now = DateTimeOffset.UtcNow;
        var expiresAt = now + effectiveLifetime;

        // 创建令牌数据
        var tokenData = new TokenData
        {
            Identity = identity,
            TokenType = tokenType,
            IssuedAt = now,
            ExpiresAt = expiresAt,
            TokenId = GenerateTokenId()
        };

        // 序列化并加密令牌
        string tokenString = SerializeAndEncryptToken(tokenData);

        // 缓存令牌
        CacheToken(tokenData.TokenId, tokenData, effectiveLifetime);

        _logger.LogInformation(
            "已生成{type}令牌，身份: {IdentityId}, 过期时间: {ExpiresAt}",
            tokenType, identity.IdentityId, expiresAt);

        return new TokenInfo
        {
            AccessToken = tokenString,
            RefreshToken = tokenType == TokenType.AccessToken ? GenerateRefreshToken(tokenData.TokenId) : null,
            TokenType = "Bearer",
            ExpiresIn = (int)effectiveLifetime.TotalSeconds,
            IssuedAt = now,
            ExpiresAt = expiresAt
        };
    }

    /// <inheritdoc />
    public TokenValidationResult ValidateToken(string token, TokenType expectedType)
    {
        try
        {
            // 解密并反序列化令牌
            var tokenData = DecryptAndDeserializeToken(token);

            // 检查令牌类型
            if (tokenData.TokenType != expectedType)
            {
                return TokenValidationResult.Failed("令牌类型不匹配");
            }

            // 检查是否过期
            if (tokenData.ExpiresAt < DateTimeOffset.UtcNow)
            {
                return TokenValidationResult.Failed("令牌已过期");
            }

            // 检查是否被吊销
            if (IsTokenRevoked(tokenData.TokenId))
            {
                return TokenValidationResult.Failed("令牌已被吊销");
            }

            _logger.LogDebug(
                "令牌验证成功: {TokenId}, 身份: {IdentityId}",
                tokenData.TokenId, tokenData.Identity?.IdentityId ?? "unknown");

            return TokenValidationResult.Success(tokenData.Identity!);
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(ex, "令牌解密失败");
            return TokenValidationResult.Failed("无效的令牌格式");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "令牌验证失败");
            return TokenValidationResult.Failed("令牌验证失败");
        }
    }

    /// <inheritdoc />
    public TokenInfo RefreshToken(string refreshToken)
    {
        // 验证刷新令牌
        var validationResult = ValidateToken(refreshToken, TokenType.RefreshToken);
        if (!validationResult.IsValid)
        {
            throw new SecurityException("无效的刷新令牌: " + validationResult.ErrorMessage);
        }

        // 获取原始访问令牌的 TokenId
        string? originalTokenId = GetOriginalTokenIdFromRefreshToken(refreshToken);
        if (originalTokenId == null)
        {
            throw new SecurityException("无法解析刷新令牌");
        }

        // 吊销原令牌
        RevokeToken(originalTokenId);

        // 生成新令牌对
        var identity = validationResult.Identity!;
        return GenerateToken(identity, TokenType.AccessToken);
    }

    /// <inheritdoc />
    public void RevokeToken(string tokenId)
    {
        _cacheLock.EnterWriteLock();
        try
        {
            if (_tokenCache.TryGetValue(tokenId, out var entry))
            {
                entry.IsRevoked = true;
                _logger.LogInformation("令牌已吊销: {TokenId}", tokenId);
            }
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }
    }

    /// <inheritdoc />
    public void CleanupExpiredTokens()
    {
        _cacheLock.EnterWriteLock();
        try
        {
            var now = DateTimeOffset.UtcNow;
            var expiredTokens = _tokenCache
                .Where(x => x.Value.ExpiresAt < now)
                .Select(x => x.Key)
                .ToList();

            foreach (var tokenId in expiredTokens)
            {
                _tokenCache.Remove(tokenId);
            }

            if (expiredTokens.Count > 0)
            {
                _logger.LogDebug("清理了 {Count} 个过期令牌", expiredTokens.Count);
            }
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }
    }

    #region 私有方法

    /// <summary>
    /// 初始化主密钥
    /// </summary>
    private byte[] InitializeMasterKey()
    {
        // 实际生产环境应从密钥管理系统获取
        byte[] key = new byte[16];
        // 使用固定种子生成确定性密钥（仅用于演示）
        byte[] seed = Encoding.UTF8.GetBytes("JZVerse.Security.MasterKey");
        using var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(seed);
        Buffer.BlockCopy(hash, 0, key, 0, 16);
        return key;
    }

    /// <summary>
    /// 生成唯一令牌 ID
    /// </summary>
    private string GenerateTokenId()
    {
        return Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// 序列化并加密令牌
    /// </summary>
    private string SerializeAndEncryptToken(TokenData tokenData)
    {
        // 序列化
        string json = JsonSerializer.Serialize(tokenData);
        byte[] plaintext = Encoding.UTF8.GetBytes(json);

        // 生成随机 IV
        byte[] iv = _sm4.GenerateIv();

        // 加密
        byte[] ciphertext = _sm4.EncryptCbc(plaintext, _masterKey, iv);

        // 组合: IV + ciphertext
        byte[] result = new byte[iv.Length + ciphertext.Length];
        Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
        Buffer.BlockCopy(ciphertext, 0, result, iv.Length, ciphertext.Length);

        // Base64 编码
        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// 解密并反序列化令牌
    /// </summary>
    private TokenData DecryptAndDeserializeToken(string token)
    {
        byte[] data = Convert.FromBase64String(token);

        // 分离 IV 和密文
        byte[] iv = data[..16];
        byte[] ciphertext = data[16..];

        // 解密
        byte[] plaintext = _sm4.DecryptCbc(ciphertext, _masterKey, iv);

        // 反序列化
        string json = Encoding.UTF8.GetString(plaintext);
        return JsonSerializer.Deserialize<TokenData>(json)!;
    }

    /// <summary>
    /// 缓存令牌
    /// </summary>
    private void CacheToken(string tokenId, TokenData tokenData, TimeSpan lifetime)
    {
        _cacheLock.EnterWriteLock();
        try
        {
            _tokenCache[tokenId] = new TokenEntry
            {
                TokenData = tokenData,
                ExpiresAt = DateTimeOffset.UtcNow + lifetime,
                IsRevoked = false
            };
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// 检查令牌是否被吊销
    /// </summary>
    private bool IsTokenRevoked(string tokenId)
    {
        _cacheLock.EnterReadLock();
        try
        {
            return _tokenCache.TryGetValue(tokenId, out var entry) && entry.IsRevoked;
        }
        finally
        {
            _cacheLock.ExitReadLock();
        }
    }

    /// <summary>
    /// 生成刷新令牌
    /// </summary>
    private string GenerateRefreshToken(string accessTokenId)
    {
        var tokenData = new TokenData
        {
            TokenId = GenerateTokenId(),
            OriginalTokenId = accessTokenId,
            TokenType = TokenType.RefreshToken,
            IssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        };

        return SerializeAndEncryptToken(tokenData);
    }

    /// <summary>
    /// 从刷新令牌获取原始访问令牌 ID
    /// </summary>
    private string? GetOriginalTokenIdFromRefreshToken(string refreshToken)
    {
        try
        {
            var tokenData = DecryptAndDeserializeToken(refreshToken);
            return tokenData.OriginalTokenId;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region 数据模型

    /// <summary>
    /// 令牌数据
    /// </summary>
    private class TokenData
    {
        public string TokenId { get; set; } = null!;
        public string? OriginalTokenId { get; set; }
        public SecurityIdentity? Identity { get; set; }
        public TokenType TokenType { get; set; }
        public DateTimeOffset IssuedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
    }

    /// <summary>
    /// 令牌缓存条目
    /// </summary>
    private class TokenEntry
    {
        public TokenData TokenData { get; set; } = null!;
        public DateTimeOffset ExpiresAt { get; set; }
        public bool IsRevoked { get; set; }
    }

    #endregion
}

/// <summary>
/// 令牌服务接口
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// 生成令牌
    /// </summary>
    TokenInfo GenerateToken(SecurityIdentity identity, TokenType tokenType, TimeSpan? lifetime = null);

    /// <summary>
    /// 验证令牌
    /// </summary>
    TokenValidationResult ValidateToken(string token, TokenType expectedType);

    /// <summary>
    /// 刷新令牌
    /// </summary>
    TokenInfo RefreshToken(string refreshToken);

    /// <summary>
    /// 吊销令牌
    /// </summary>
    void RevokeToken(string tokenId);

    /// <summary>
    /// 清理过期令牌
    /// </summary>
    void CleanupExpiredTokens();
}

/// <summary>
/// 令牌验证结果
/// </summary>
public sealed record TokenValidationResult
{
    /// <summary>
    /// 是否有效
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// 身份信息
    /// </summary>
    public SecurityIdentity? Identity { get; init; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static TokenValidationResult Success(SecurityIdentity identity)
    {
        return new TokenValidationResult
        {
            IsValid = true,
            Identity = identity
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static TokenValidationResult Failed(string message)
    {
        return new TokenValidationResult
        {
            IsValid = false,
            ErrorMessage = message
        };
    }
}

/// <summary>
/// 安全异常
/// </summary>
public class SecurityException : Exception
{
    public SecurityException(string message) : base(message) { }
    public SecurityException(string message, Exception innerException) : base(message, innerException) { }
}
