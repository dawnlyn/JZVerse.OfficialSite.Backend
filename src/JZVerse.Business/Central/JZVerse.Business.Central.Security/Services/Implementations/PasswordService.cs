using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using JZVerse.Business.Central.Security.Services.Interfaces;
using JZVerse.MicroHuaxia.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace JZVerse.Business.Central.Security.Services.Implementations;

/// <summary>
/// 密码服务实现
/// </summary>
public sealed class PasswordService : IPasswordService
{
    private readonly ISm3Provider? _sm3Provider;
    private readonly ILogger<PasswordService> _logger;

    // 配置常量
    private const int SaltLength = 32;
    private const int MinPasswordLength = 8;
    private const int HashIterations = 10000;

    // 密码哈希格式: 算法标识$盐值$哈希值
    private const string FormatSm3 = "SM3";
    private const string FormatMd5Salt = "MD5";

    public PasswordService(ILogger<PasswordService> logger, ISm3Provider? sm3Provider = null)
    {
        _logger = logger;
        _sm3Provider = sm3Provider;
    }

    /// <inheritdoc />
    public Task<string> HashPasswordAsync(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("密码不能为空", nameof(password));
        }

        try
        {
            var salt = GenerateSalt(SaltLength);
            string hash;
            string format;

            if (_sm3Provider != null)
            {
                // 使用SM3国密算法 (HMAC-SM3)
                var saltBytes = Convert.FromHexString(salt);
                var passwordBytes = Encoding.UTF8.GetBytes(password);
                var hashBytes = _sm3Provider.Hmac(passwordBytes, saltBytes);
                hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
                format = FormatSm3;
            }
            else
            {
                // 降级到MD5+盐
                hash = ComputeMd5Hash(password, salt);
                format = FormatMd5Salt;
            }

            // 格式: 算法$盐值$哈希值
            var result = $"{format}${salt}${hash}";
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "密码哈希计算失败");
            throw;
        }
    }

    /// <inheritdoc />
    public Task<bool> VerifyPasswordAsync(string password, string hashedPassword)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return Task.FromResult(false);
        }

        if (string.IsNullOrWhiteSpace(hashedPassword))
        {
            return Task.FromResult(false);
        }

        try
        {
            // 解析哈希格式
            var parts = hashedPassword.Split('$');
            if (parts.Length != 3)
            {
                _logger.LogWarning("密码哈希格式无效");
                return Task.FromResult(false);
            }

            var format = parts[0];
            var salt = parts[1];
            var storedHash = parts[2];

            string computedHash;

            switch (format.ToUpperInvariant())
            {
                case FormatSm3 when _sm3Provider != null:
                    var saltBytes = Convert.FromHexString(salt);
                    var passwordBytes = Encoding.UTF8.GetBytes(password);
                    var hashBytes = _sm3Provider.Hmac(passwordBytes, saltBytes);
                    computedHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
                    break;

                case FormatMd5Salt:
                    computedHash = ComputeMd5Hash(password, salt);
                    break;

                default:
                    _logger.LogWarning("不支持的哈希算法: {Format}", format);
                    return Task.FromResult(false);
            }

            // 使用固定时间比较防止时序攻击
            var isValid = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedHash),
                Encoding.UTF8.GetBytes(storedHash));

            return Task.FromResult(isValid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "密码验证失败");
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public string GenerateSalt(int length = 32)
    {
        if (length < 16 || length > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "盐值长度应在16-64之间");
        }

        var bytes = new byte[length / 2]; // 每字节转换为2个十六进制字符
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <inheritdoc />
    public PasswordStrength CheckPasswordStrength(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return PasswordStrength.VeryWeak;
        }

        int score = 0;

        // 长度检查
        if (password.Length >= MinPasswordLength) score += 10;
        if (password.Length >= 12) score += 10;
        if (password.Length >= 16) score += 10;

        // 字符类型检查
        if (Regex.IsMatch(password, @"[a-z]")) score += 10; // 小写字母
        if (Regex.IsMatch(password, @"[A-Z]")) score += 15; // 大写字母
        if (Regex.IsMatch(password, @"[0-9]")) score += 15; // 数字
        if (Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]")) score += 20; // 特殊字符

        // 复杂度检查
        var uniqueChars = password.Distinct().Count();
        if (uniqueChars >= password.Length * 0.7) score += 10; // 字符多样性

        // 常见弱密码检查
        if (IsCommonWeakPassword(password)) score = Math.Min(score, 20);

        return score switch
        {
            >= 90 => PasswordStrength.VeryStrong,
            >= 75 => PasswordStrength.Strong,
            >= 50 => PasswordStrength.Medium,
            >= 30 => PasswordStrength.Weak,
            _ => PasswordStrength.VeryWeak
        };
    }

    /// <inheritdoc />
    public string GenerateRandomPassword(int length = 16, bool includeSpecialChars = true)
    {
        if (length < 8)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "密码长度至少为8位");
        }

        const string lowerChars = "abcdefghijklmnopqrstuvwxyz";
        const string upperChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string numberChars = "0123456789";
        const string specialChars = "!@#$%^&*()_+-=[]{}|;:,.<>?";

        var allChars = lowerChars + upperChars + numberChars;
        if (includeSpecialChars)
        {
            allChars += specialChars;
        }

        var password = new char[length];
        using (var rng = RandomNumberGenerator.Create())
        {
            // 确保包含每种类型
            password[0] = lowerChars[GetRandomInt(rng, lowerChars.Length)];
            password[1] = upperChars[GetRandomInt(rng, upperChars.Length)];
            password[2] = numberChars[GetRandomInt(rng, numberChars.Length)];

            var nextIndex = 3;
            if (includeSpecialChars)
            {
                password[3] = specialChars[GetRandomInt(rng, specialChars.Length)];
                nextIndex = 4;
            }

            // 填充剩余字符
            for (int i = nextIndex; i < length; i++)
            {
                password[i] = allChars[GetRandomInt(rng, allChars.Length)];
            }

            // 打乱顺序
            Shuffle(password, rng);
        }

        return new string(password);
    }

    private static string ComputeMd5Hash(string password, string salt)
    {
        using var md5 = MD5.Create();
        var input = salt + password + salt;
        var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static int GetRandomInt(RandomNumberGenerator rng, int max)
    {
        var bytes = new byte[4];
        rng.GetBytes(bytes);
        var value = BitConverter.ToInt32(bytes, 0) & int.MaxValue;
        return value % max;
    }

    private static void Shuffle(char[] array, RandomNumberGenerator rng)
    {
        for (int i = array.Length - 1; i > 0; i--)
        {
            var j = GetRandomInt(rng, i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
    }

    private static bool IsCommonWeakPassword(string password)
    {
        var lowerPassword = password.ToLowerInvariant();
        var commonPasswords = new[]
        {
            "123456", "password", "12345678", "qwerty", "123456789",
            "letmein", "1234567", "football", "iloveyou", "admin",
            "welcome", "monkey", "login", "abc123", "111111",
            "123123", "password123", "admin123", "root", "toor"
        };

        return commonPasswords.Any(p => lowerPassword.Contains(p));
    }
}
