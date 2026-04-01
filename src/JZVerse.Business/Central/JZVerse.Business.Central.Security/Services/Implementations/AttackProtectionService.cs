using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using JZVerse.Business.Central.Security.Database;
using JZVerse.Business.Central.Security.Database.Models;
using JZVerse.Business.Central.Security.Services.Interfaces;
using JZVerse.MicroHuaxia.DataAccess.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.Business.Central.Security.Services.Implementations;

/// <summary>
/// 攻击防护服务实现
/// </summary>
public sealed class AttackProtectionService : IAttackProtectionService
{
    private readonly IDbExecutor _db;
    private readonly ILogger<AttackProtectionService> _logger;
    private readonly SecurityOptions _options;

    // IP封禁内存缓存
    private readonly ConcurrentDictionary<string, IpBanCacheEntry> _ipBanCache = new();

    // CSRF Token密钥
    private const string CsrfSecretKey = "JZVerse-CSRF-Secret-Key-2024";

    // SQL注入检测正则
    private static readonly Regex[] SqlInjectionPatterns = new[]
    {
        // 基本SQL关键字
        new Regex(@"(\b(SELECT|INSERT|UPDATE|DELETE|DROP|CREATE|ALTER|TRUNCATE|EXEC|EXECUTE|UNION|UNION\s+ALL)\b)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // 注释符号
        new Regex(@"(--|#|/\*|\*/)", RegexOptions.Compiled),
        // 布尔逻辑
        new Regex(@"(\b(OR|AND)\b\s+\d+\s*=\s*\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // 分号和引号组合
        new Regex(@"(;\s*['""\s]*\s*(SELECT|INSERT|UPDATE|DELETE|DROP))", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // 时间延迟注入
        new Regex(@"(WAITFOR\s+DELAY|SLEEP\s*\(|BENCHMARK\s*\()", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // 堆叠查询
        new Regex(@"(;\s*(SELECT|INSERT|UPDATE|DELETE|DROP|EXEC|EXECUTE))", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    // SQL关键字列表（用于额外检查）
    private static readonly string[] SqlKeywords = new[]
    {
        "SELECT", "INSERT", "UPDATE", "DELETE", "DROP", "CREATE", "ALTER", "TRUNCATE",
        "EXEC", "EXECUTE", "UNION", "UNION ALL", "WHERE", "FROM", "ORDER BY", "GROUP BY",
        "HAVING", "JOIN", "LEFT JOIN", "RIGHT JOIN", "INNER JOIN", "OUTER JOIN"
    };

    // XSS检测正则
    private static readonly Regex[] XssPatterns = new[]
    {
        // script标签
        new Regex(@"(<script[^>]*>[\s\S]*?</script>)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // iframe标签
        new Regex(@"(<iframe[^>]*>[\s\S]*?</iframe>)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // object/embed标签
        new Regex(@"<(object|embed)[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // 事件处理器 - 修正引号问题
        new Regex(@"\s(on\w+)\s*=\s*[\x27\x22]?[^\x27\x22>\s]*[\x27\x22]?", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // javascript伪协议
        new Regex(@"(javascript:|data:|vbscript:|mocha:|livescript:)\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // HTML实体编码的script
        new Regex(@"(&#x?\d+;?|%[0-9a-fA-F]{2}){2,}", RegexOptions.Compiled),
        // expression
        new Regex(@"(expression\s*\(|@import\s*|\.innerHTML|\.outerHTML)", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    // 危险事件处理器
    private static readonly string[] DangerousEventHandlers = new[]
    {
        "onerror", "onload", "onclick", "ondblclick", "onmousedown", "onmouseup",
        "onmouseover", "onmousemove", "onmouseout", "onkeypress", "onkeydown",
        "onkeyup", "onfocus", "onblur", "onchange", "onsubmit", "onreset",
        "onselect", "onabort", "onbeforeunload", "onbeforeprint", "onafterprint"
    };

    // 路径遍历检测正则
    private static readonly Regex[] PathTraversalPatterns = new[]
    {
        new Regex(@"\.\./", RegexOptions.Compiled),
        new Regex(@"\.\.\\", RegexOptions.Compiled),
        new Regex(@"%2e%2e[/\\]", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"\.{2,}[/\\]", RegexOptions.Compiled),
        new Regex(@"%252e%252e", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    // 命令注入检测正则
    private static readonly Regex[] CommandInjectionPatterns = new[]
    {
        new Regex(@"[;&|`]\s*\w+", RegexOptions.Compiled),
        new Regex(@"\$\([^)]+\)", RegexOptions.Compiled),
        new Regex(@"`[^`]+`", RegexOptions.Compiled),
        new Regex(@"\|\s*\w+", RegexOptions.Compiled),
        new Regex(@"(cmd|command|bash|sh|powershell|pwsh)\s+/[cik]", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    public AttackProtectionService(
        IDbExecutor db,
        ILogger<AttackProtectionService> logger,
        IOptions<SecurityOptions> options)
    {
        _db = db;
        _logger = logger;
        _options = options.Value;
    }

    #region SQL注入检测

    /// <inheritdoc />
    public AttackCheckResult CheckSqlInjection(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return AttackCheckResult.Safe();
        }

        // 正则检测
        foreach (var pattern in SqlInjectionPatterns)
        {
            var match = pattern.Match(input);
            if (match.Success)
            {
                return AttackCheckResult.Detected(
                    AttackTypes.SqlInjection,
                    RiskLevels.High,
                    $"检测到SQL注入攻击特征: {match.Value}",
                    pattern.ToString(),
                    null,
                    match.Value);
            }
        }

        // 关键字检测（防止简单的关键字绕过）
        var upperInput = input.ToUpperInvariant();
        foreach (var keyword in SqlKeywords)
        {
            if (upperInput.Contains(keyword))
            {
                // 检查是否伴随特殊字符
                if (input.Contains("'", StringComparison.Ordinal) ||
                    input.Contains("\"", StringComparison.Ordinal) ||
                    input.Contains(";", StringComparison.Ordinal) ||
                    input.Contains("--", StringComparison.Ordinal))
                {
                    return AttackCheckResult.Detected(
                        AttackTypes.SqlInjection,
                        RiskLevels.Medium,
                        $"检测到可疑SQL关键字组合: {keyword}",
                        keyword,
                        null,
                        input.Length > 50 ? input[..50] + "..." : input);
                }
            }
        }

        return AttackCheckResult.Safe();
    }

    /// <inheritdoc />
    public List<AttackCheckResult> CheckSqlInjectionBatch(Dictionary<string, string> inputs)
    {
        var results = new List<AttackCheckResult>();

        foreach (var (fieldName, value) in inputs)
        {
            var result = CheckSqlInjection(value);
            if (result.IsAttack)
            {
                result.FieldName = fieldName;
                results.Add(result);
            }
        }

        return results;
    }

    #endregion

    #region XSS检测

    /// <inheritdoc />
    public AttackCheckResult CheckXss(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return AttackCheckResult.Safe();
        }

        // 正则检测
        foreach (var pattern in XssPatterns)
        {
            var match = pattern.Match(input);
            if (match.Success)
            {
                return AttackCheckResult.Detected(
                    AttackTypes.Xss,
                    RiskLevels.High,
                    $"检测到XSS攻击特征: {match.Value}",
                    pattern.ToString(),
                    null,
                    match.Value.Length > 50 ? match.Value[..50] + "..." : match.Value);
            }
        }

        // 事件处理器检测
        var lowerInput = input.ToLowerInvariant();
        foreach (var handler in DangerousEventHandlers)
        {
            if (lowerInput.Contains(handler))
            {
                return AttackCheckResult.Detected(
                    AttackTypes.Xss,
                    RiskLevels.Medium,
                    $"检测到危险事件处理器: {handler}",
                    handler,
                    null,
                    input.Length > 50 ? input[..50] + "..." : input);
            }
        }

        return AttackCheckResult.Safe();
    }

    /// <inheritdoc />
    public string HtmlEncode(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        return HttpUtility.HtmlEncode(input);
    }

    /// <inheritdoc />
    public string HtmlDecode(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        return HttpUtility.HtmlDecode(input);
    }

    #endregion

    #region CSRF检测

    /// <inheritdoc />
    public string GenerateCsrfToken(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            throw new ArgumentException("Session ID cannot be empty", nameof(sessionId));
        }

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var data = $"{sessionId}:{timestamp}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(CsrfSecretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        var token = Convert.ToBase64String(hash);

        return $"{timestamp}:{token}";
    }

    /// <inheritdoc />
    public bool ValidateCsrfToken(string token, string sessionId)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(sessionId))
        {
            return false;
        }

        var parts = token.Split(':', 2);
        if (parts.Length != 2)
        {
            return false;
        }

        var timestampStr = parts[0];
        var hashValue = parts[1];

        // 验证时间戳（Token有效期2小时）
        if (!long.TryParse(timestampStr, out var timestamp))
        {
            return false;
        }

        var tokenTime = DateTimeOffset.FromUnixTimeSeconds(timestamp);
        if (DateTimeOffset.UtcNow - tokenTime > TimeSpan.FromHours(2))
        {
            return false;
        }

        // 验证签名
        var data = $"{sessionId}:{timestamp}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(CsrfSecretKey));
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        var computedToken = Convert.ToBase64String(computedHash);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(hashValue),
            Encoding.UTF8.GetBytes(computedToken));
    }

    /// <inheritdoc />
    public AttackCheckResult CheckCsrfRisk(string? referer, string? origin, List<string> allowedDomains)
    {
        // 如果没有Referer和Origin，可能存在CSRF风险
        if (string.IsNullOrEmpty(referer) && string.IsNullOrEmpty(origin))
        {
            return AttackCheckResult.Detected(
                AttackTypes.Csrf,
                RiskLevels.Medium,
                "请求缺少Referer和Origin头，可能存在CSRF风险",
                "MissingHeaders",
                null,
                null);
        }

        // 检查Origin
        if (!string.IsNullOrEmpty(origin))
        {
            if (!IsDomainAllowed(origin, allowedDomains))
            {
                return AttackCheckResult.Detected(
                    AttackTypes.Csrf,
                    RiskLevels.High,
                    $"不合法的Origin: {origin}",
                    "InvalidOrigin",
                    null,
                    origin);
            }
        }

        // 检查Referer
        if (!string.IsNullOrEmpty(referer))
        {
            if (!IsDomainAllowed(referer, allowedDomains))
            {
                return AttackCheckResult.Detected(
                    AttackTypes.Csrf,
                    RiskLevels.High,
                    $"不合法的Referer: {referer}",
                    "InvalidReferer",
                    null,
                    referer);
            }
        }

        return AttackCheckResult.Safe();
    }

    private static bool IsDomainAllowed(string url, List<string> allowedDomains)
    {
        if (allowedDomains.Count == 0)
        {
            return true;
        }

        try
        {
            var uri = new Uri(url);
            var domain = uri.Host.ToLowerInvariant();

            return allowedDomains.Any(allowed =>
                domain == allowed.ToLowerInvariant() ||
                domain.EndsWith($".{allowed.ToLowerInvariant()}", StringComparison.Ordinal));
        }
        catch
        {
            // 如果不是有效URL，直接比较字符串
            var lowerUrl = url.ToLowerInvariant();
            return allowedDomains.Any(allowed =>
                lowerUrl.Contains(allowed.ToLowerInvariant()));
        }
    }

    #endregion

    #region 路径遍历检测

    /// <inheritdoc />
    public AttackCheckResult CheckPathTraversal(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return AttackCheckResult.Safe();
        }

        foreach (var pattern in PathTraversalPatterns)
        {
            var match = pattern.Match(path);
            if (match.Success)
            {
                return AttackCheckResult.Detected(
                    AttackTypes.PathTraversal,
                    RiskLevels.High,
                    $"检测到路径遍历攻击特征: {match.Value}",
                    pattern.ToString(),
                    null,
                    path.Length > 100 ? path[..100] + "..." : path);
            }
        }

        // 检测空字节攻击
        if (path.Contains('\0'))
        {
            return AttackCheckResult.Detected(
                AttackTypes.PathTraversal,
                RiskLevels.High,
                "检测到空字节注入攻击",
                "NullByte",
                null,
                path.Length > 100 ? path[..100] + "..." : path);
        }

        return AttackCheckResult.Safe();
    }

    #endregion

    #region 命令注入检测

    /// <inheritdoc />
    public AttackCheckResult CheckCommandInjection(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return AttackCheckResult.Safe();
        }

        foreach (var pattern in CommandInjectionPatterns)
        {
            var match = pattern.Match(input);
            if (match.Success)
            {
                return AttackCheckResult.Detected(
                    AttackTypes.CommandInjection,
                    RiskLevels.High,
                    $"检测到命令注入攻击特征: {match.Value}",
                    pattern.ToString(),
                    null,
                    match.Value);
            }
        }

        return AttackCheckResult.Safe();
    }

    #endregion

    #region IP封禁

    /// <inheritdoc />
    public async Task<bool> BanIpAsync(string ipAddress, string reason, int expireMinutes = 0)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return false;
            }

            var now = DateTime.UtcNow;
            var expireAt = expireMinutes > 0 ? now.AddMinutes(expireMinutes) : (DateTime?)null;

            // 添加到内存缓存
            var cacheEntry = new IpBanCacheEntry
            {
                IpAddress = ipAddress,
                Reason = reason,
                BannedAt = now,
                ExpireAt = expireAt,
                AttackCount = 1
            };

            _ipBanCache[ipAddress] = cacheEntry;

            // 添加到数据库
            const string sql = @"
                INSERT INTO security_ip_bans (id, ip_address, reason, banned_at, expire_at, attack_count, created_at)
                VALUES (@Id, @IpAddress, @Reason, @BannedAt, @ExpireAt, @AttackCount, @CreatedAt)
                ON CONFLICT (ip_address) 
                DO UPDATE SET 
                    reason = EXCLUDED.reason,
                    expire_at = EXCLUDED.expire_at,
                    attack_count = security_ip_bans.attack_count + 1,
                    updated_at = @CreatedAt";

            await _db.ExecuteAsync(sql, new
            {
                Id = Guid.NewGuid(),
                IpAddress = ipAddress,
                Reason = reason,
                BannedAt = now,
                ExpireAt = expireAt,
                AttackCount = 1,
                CreatedAt = now
            });

            _logger.LogWarning(
                "IP已封禁 - IP: {IpAddress}, 原因: {Reason}, 过期时间: {ExpireAt}",
                ipAddress, reason, expireAt?.ToString() ?? "永久");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "封禁IP失败 - IP: {IpAddress}", ipAddress);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> UnbanIpAsync(string ipAddress)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return false;
            }

            // 移除内存缓存
            _ipBanCache.TryRemove(ipAddress, out _);

            // 从数据库删除
            const string sql = "DELETE FROM security_ip_bans WHERE ip_address = @IpAddress";
            var affected = await _db.ExecuteAsync(sql, new { IpAddress = ipAddress });

            _logger.LogInformation("IP已解封 - IP: {IpAddress}", ipAddress);

            return affected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解封IP失败 - IP: {IpAddress}", ipAddress);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<IpBanInfo?> CheckIpBannedAsync(string ipAddress)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return null;
            }

            // 先检查内存缓存
            if (_ipBanCache.TryGetValue(ipAddress, out var cached))
            {
                if (cached.ExpireAt == null || cached.ExpireAt > DateTime.UtcNow)
                {
                    return new IpBanInfo
                    {
                        IpAddress = cached.IpAddress,
                        Reason = cached.Reason,
                        BannedAt = cached.BannedAt,
                        ExpireAt = cached.ExpireAt,
                        AttackCount = cached.AttackCount
                    };
                }

                // 缓存已过期，移除
                _ipBanCache.TryRemove(ipAddress, out _);
            }

            // 查询数据库
            const string sql = @"
                SELECT ip_address AS IpAddress, reason AS Reason, banned_at AS BannedAt, 
                       expire_at AS ExpireAt, attack_count AS AttackCount
                FROM security_ip_bans 
                WHERE ip_address = @IpAddress 
                AND (expire_at IS NULL OR expire_at > @Now)
                LIMIT 1";

            var result = await _db.QueryFirstOrDefaultAsync<IpBanInfo>(sql, new
            {
                IpAddress = ipAddress,
                Now = DateTime.UtcNow
            });

            if (result != null)
            {
                // 更新缓存
                _ipBanCache[ipAddress] = new IpBanCacheEntry
                {
                    IpAddress = result.IpAddress,
                    Reason = result.Reason,
                    BannedAt = result.BannedAt,
                    ExpireAt = result.ExpireAt,
                    AttackCount = result.AttackCount
                };
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查IP封禁状态失败 - IP: {IpAddress}", ipAddress);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<(List<IpBanInfo> Items, int Total)> GetBannedIpsAsync(int pageIndex = 1, int pageSize = 20)
    {
        try
        {
            var offset = (pageIndex - 1) * pageSize;

            const string querySql = @"
                SELECT ip_address AS IpAddress, reason AS Reason, banned_at AS BannedAt, 
                       expire_at AS ExpireAt, attack_count AS AttackCount
                FROM security_ip_bans 
                WHERE expire_at IS NULL OR expire_at > @Now
                ORDER BY banned_at DESC
                LIMIT @Limit OFFSET @Offset";

            const string countSql = @"
                SELECT COUNT(*) FROM security_ip_bans 
                WHERE expire_at IS NULL OR expire_at > @Now";

            var items = await _db.QueryAsync<IpBanInfo>(querySql, new
            {
                Now = DateTime.UtcNow,
                Limit = pageSize,
                Offset = offset
            });

            var total = await _db.ExecuteScalarAsync<int>(countSql, new { Now = DateTime.UtcNow });

            return (items.ToList(), total);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取封禁IP列表失败");
            return (new List<IpBanInfo>(), 0);
        }
    }

    #endregion

    #region 综合检测

    /// <inheritdoc />
    public async Task<List<AttackCheckResult>> ComprehensiveCheckAsync(SecurityRequest request)
    {
        var results = new List<AttackCheckResult>();

        // 检查IP是否被封禁
        var banInfo = await CheckIpBannedAsync(request.IpAddress);
        if (banInfo != null)
        {
            results.Add(AttackCheckResult.Detected(
                AttackTypes.Other,
                RiskLevels.High,
                $"IP已被封禁: {banInfo.Reason}",
                "IpBanned",
                "ipAddress",
                request.IpAddress));
            return results;
        }

        // SQL注入检测
        foreach (var (key, value) in request.QueryParams)
        {
            var result = CheckSqlInjection(value);
            if (result.IsAttack)
            {
                result.FieldName = $"query.{key}";
                results.Add(result);
            }
        }

        foreach (var (key, value) in request.FormData)
        {
            var result = CheckSqlInjection(value);
            if (result.IsAttack)
            {
                result.FieldName = $"form.{key}";
                results.Add(result);
            }
        }

        if (!string.IsNullOrEmpty(request.Body))
        {
            var bodyResult = CheckSqlInjection(request.Body);
            if (bodyResult.IsAttack)
            {
                bodyResult.FieldName = "body";
                results.Add(bodyResult);
            }
        }

        // XSS检测
        foreach (var (key, value) in request.QueryParams)
        {
            var result = CheckXss(value);
            if (result.IsAttack)
            {
                result.FieldName = $"query.{key}";
                results.Add(result);
            }
        }

        foreach (var (key, value) in request.FormData)
        {
            var result = CheckXss(value);
            if (result.IsAttack)
            {
                result.FieldName = $"form.{key}";
                results.Add(result);
            }
        }

        // 路径遍历检测
        var pathResult = CheckPathTraversal(request.Path);
        if (pathResult.IsAttack)
        {
            results.Add(pathResult);
        }

        // 命令注入检测
        if (!string.IsNullOrEmpty(request.Body))
        {
            var cmdResult = CheckCommandInjection(request.Body);
            if (cmdResult.IsAttack)
            {
                cmdResult.FieldName = "body";
                results.Add(cmdResult);
            }
        }

        // 记录攻击日志
        foreach (var result in results.Where(r => r.IsAttack))
        {
            await LogAttackAsync(request, result);
        }

        return results;
    }

    private async Task LogAttackAsync(SecurityRequest request, AttackCheckResult result)
    {
        try
        {
            var attackLog = new AttackLog
            {
                Id = Guid.NewGuid(),
                AttackType = result.AttackType,
                SourceIp = request.IpAddress,
                TargetUrl = request.Path,
                HttpMethod = request.Method,
                RequestData = !string.IsNullOrEmpty(request.Body) ? request.Body[..Math.Min(2000, request.Body.Length)] : null,
                RequestHeaders = System.Text.Json.JsonSerializer.Serialize(request.Headers),
                AttackPattern = result.Pattern,
                RiskLevel = result.RiskLevel,
                IsIntercepted = true,
                InterceptAction = "Blocked",
                UserAgent = request.UserAgent,
                IsHandled = false,
                CreatedAt = DateTime.UtcNow
            };

            await _db.ExecuteAsync(SecuritySql.InsertAttackLog, attackLog);

            _logger.LogWarning(
                "攻击已拦截 - 类型: {AttackType}, 来源IP: {IpAddress}, 路径: {Path}, 特征: {Pattern}",
                result.AttackType, request.IpAddress, request.Path, result.Pattern);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "记录攻击日志失败");
        }
    }

    #endregion

    #region IP封禁缓存条目

    private class IpBanCacheEntry
    {
        public string IpAddress { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime BannedAt { get; set; }
        public DateTime? ExpireAt { get; set; }
        public int AttackCount { get; set; }
    }

    #endregion
}
