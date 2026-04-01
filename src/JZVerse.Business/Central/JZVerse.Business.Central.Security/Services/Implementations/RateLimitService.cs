using System.Collections.Concurrent;
using System.Text.Json;
using JZVerse.Business.Central.Security.Database;
using JZVerse.Business.Central.Security.Database.Models;
using JZVerse.Business.Central.Security.Services.Interfaces;
using JZVerse.MicroHuaxia.DataAccess.Abstractions;
using Microsoft.Extensions.Logging;

namespace JZVerse.Business.Central.Security.Services.Implementations;

/// <summary>
/// 限流服务实现
/// </summary>
public sealed class RateLimitService : IRateLimitService
{
    private readonly IDbExecutor _db;
    private readonly ILogger<RateLimitService> _logger;

    // 内存缓存：键 -> (限流记录, 过期时间)
    private readonly ConcurrentDictionary<string, RateLimitCacheEntry> _cache = new();

    // 缓存过期扫描间隔
    private readonly TimeSpan _cacheCleanupInterval = TimeSpan.FromMinutes(5);
    private DateTime _lastCleanupTime = DateTime.UtcNow;

    // 内存缓存最大条目数
    private const int MaxCacheSize = 10000;

    public RateLimitService(IDbExecutor db, ILogger<RateLimitService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<RateLimitCheckResult> CheckFixedWindowAsync(
        string keyType,
        string keyValue,
        int limit,
        int windowSeconds)
    {
        try
        {
            var now = DateTime.UtcNow;
            var windowStart = GetWindowStart(now, windowSeconds);
            var windowEnd = windowStart.AddSeconds(windowSeconds);
            var cacheKey = GetCacheKey(keyType, keyValue, windowStart);

            // 尝试从缓存获取
            var cached = GetFromCache(cacheKey);
            if (cached is null)
            {
                // 从数据库获取或创建新记录
                cached = await GetOrCreateFromDbAsync(keyType, keyValue, windowStart, windowEnd, now);
                AddToCache(cacheKey, cached, windowEnd);
            }

            // 检查是否已限流
            if (cached.IsLimited)
            {
                cached.LimitTriggeredCount++;
                await UpdateToDbAsync(cached);

                _logger.LogWarning(
                    "限流触发 - KeyType: {KeyType}, KeyValue: {KeyValue}, Count: {Count}, Limit: {Limit}",
                    keyType, keyValue, cached.RequestCount, limit);

                return RateLimitCheckResult.Denied(limit, windowEnd, cached.RequestCount);
            }

            // 检查是否超过限制
            if (cached.RequestCount >= limit)
            {
                cached.IsLimited = true;
                cached.LimitTriggeredCount++;
                await UpdateToDbAsync(cached);

                _logger.LogWarning(
                    "限流触发 - KeyType: {KeyType}, KeyValue: {KeyValue}, Count: {Count}, Limit: {Limit}",
                    keyType, keyValue, cached.RequestCount, limit);

                return RateLimitCheckResult.Denied(limit, windowEnd, cached.RequestCount);
            }

            // 允许请求，增加计数
            cached.RequestCount++;
            cached.LastRequestAt = now;
            await UpdateToDbAsync(cached);

            // 更新缓存
            AddToCache(cacheKey, cached, windowEnd);

            // 清理过期缓存
            CleanupExpiredCacheIfNeeded();

            return RateLimitCheckResult.Allowed(
                limit - cached.RequestCount,
                limit,
                windowEnd,
                cached.RequestCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "固定窗口限流检查失败 - KeyType: {KeyType}, KeyValue: {KeyValue}", keyType, keyValue);
            // 出错时允许请求，避免阻塞正常流量
            return RateLimitCheckResult.Allowed(1, limit, DateTime.UtcNow.AddSeconds(windowSeconds), 0);
        }
    }

    /// <inheritdoc />
    public async Task<RateLimitCheckResult> CheckSlidingWindowAsync(
        string keyType,
        string keyValue,
        int limit,
        int windowSeconds)
    {
        try
        {
            var now = DateTime.UtcNow;
            var windowStart = now.AddSeconds(-windowSeconds);
            var windowEnd = now;
            var cacheKey = GetCacheKey(keyType, keyValue, now);

            // 滑动窗口：获取当前窗口内的请求数
            var currentCount = await GetSlidingWindowCountAsync(keyType, keyValue, windowStart, now);

            // 检查是否超过限制
            if (currentCount >= limit)
            {
                var resetTime = now.AddSeconds(windowSeconds);

                _logger.LogWarning(
                    "滑动窗口限流触发 - KeyType: {KeyType}, KeyValue: {KeyValue}, Count: {Count}, Limit: {Limit}",
                    keyType, keyValue, currentCount, limit);

                return RateLimitCheckResult.Denied(limit, resetTime, currentCount);
            }

            // 记录当前请求
            var record = new RateLimitRecord
            {
                Id = Guid.NewGuid(),
                KeyType = keyType,
                KeyValue = keyValue,
                RequestCount = 1,
                WindowStart = now,
                WindowEnd = now.AddSeconds(1), // 滑动窗口每条记录有效期1秒
                LastRequestAt = now,
                CreatedAt = now,
                UpdatedAt = now
            };

            // 使用独立的小窗口记录每个请求
            await _db.ExecuteAsync(SecuritySql.UpsertRateLimit, record);

            var remaining = limit - currentCount - 1;
            var resetTime2 = now.AddSeconds(windowSeconds);

            return RateLimitCheckResult.Allowed(
                Math.Max(0, remaining),
                limit,
                resetTime2,
                currentCount + 1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "滑动窗口限流检查失败 - KeyType: {KeyType}, KeyValue: {KeyValue}", keyType, keyValue);
            // 出错时允许请求
            return RateLimitCheckResult.Allowed(1, limit, DateTime.UtcNow.AddSeconds(windowSeconds), 0);
        }
    }

    /// <inheritdoc />
    public Task<RateLimitCheckResult> CheckIpLimitAsync(string ipAddress, int limit, int windowSeconds)
    {
        return CheckFixedWindowAsync(RateLimitKeyTypes.Ip, ipAddress, limit, windowSeconds);
    }

    /// <inheritdoc />
    public Task<RateLimitCheckResult> CheckUserLimitAsync(Guid userId, int limit, int windowSeconds)
    {
        return CheckFixedWindowAsync(RateLimitKeyTypes.User, userId.ToString(), limit, windowSeconds);
    }

    /// <inheritdoc />
    public Task<RateLimitCheckResult> CheckEndpointLimitAsync(string endpoint, int limit, int windowSeconds)
    {
        return CheckFixedWindowAsync(RateLimitKeyTypes.Endpoint, endpoint, limit, windowSeconds);
    }

    /// <inheritdoc />
    public async Task<int> RecordRequestAsync(string keyType, string keyValue, int windowSeconds)
    {
        try
        {
            var now = DateTime.UtcNow;
            var windowStart = GetWindowStart(now, windowSeconds);
            var windowEnd = windowStart.AddSeconds(windowSeconds);

            var record = await GetOrCreateFromDbAsync(keyType, keyValue, windowStart, windowEnd, now);
            record.RequestCount++;
            record.LastRequestAt = now;
            record.UpdatedAt = now;

            await UpdateToDbAsync(record);

            var cacheKey = GetCacheKey(keyType, keyValue, windowStart);
            AddToCache(cacheKey, record, windowEnd);

            return record.RequestCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "记录限流请求失败 - KeyType: {KeyType}, KeyValue: {KeyValue}", keyType, keyValue);
            return 0;
        }
    }

    /// <inheritdoc />
    public async Task<int> CleanExpiredRecordsAsync()
    {
        try
        {
            var now = DateTime.UtcNow;

            // 清理数据库过期记录
            var affected = await _db.ExecuteAsync(SecuritySql.DeleteExpiredRateLimits, new { Now = now });

            // 清理内存缓存
            var cacheCount = CleanupExpiredCache();

            _logger.LogInformation(
                "清理过期限流记录完成 - 数据库: {DbCount}, 缓存: {CacheCount}",
                affected, cacheCount);

            return affected + cacheCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理过期限流记录失败");
            return 0;
        }
    }

    /// <inheritdoc />
    public async Task<RateLimitStatus?> GetRateLimitStatusAsync(string keyType, string keyValue)
    {
        try
        {
            // 优先从缓存获取
            var cacheKey = _cache.Keys.FirstOrDefault(k => k.StartsWith($"{keyType}:{keyValue}:"));
            if (cacheKey is not null && _cache.TryGetValue(cacheKey, out var entry))
            {
                if (entry.ExpiresAt > DateTime.UtcNow)
                {
                    return new RateLimitStatus
                    {
                        KeyType = keyType,
                        KeyValue = keyValue,
                        RequestCount = entry.Record.RequestCount,
                        WindowStart = entry.Record.WindowStart,
                        WindowEnd = entry.Record.WindowEnd,
                        IsLimited = entry.Record.IsLimited,
                        LimitTriggeredCount = entry.Record.LimitTriggeredCount
                    };
                }
            }

            // 从数据库获取最新的记录
            var now = DateTime.UtcNow;
            const string sql = @"
                SELECT * FROM security_rate_limits 
                WHERE key_type = @KeyType AND key_value = @KeyValue 
                AND window_end > @Now
                ORDER BY window_start DESC
                LIMIT 1";

            var record = await _db.QueryFirstOrDefaultAsync<RateLimitRecord>(
                sql,
                new { KeyType = keyType, KeyValue = keyValue, Now = now });

            if (record is null)
            {
                return null;
            }

            return new RateLimitStatus
            {
                KeyType = keyType,
                KeyValue = keyValue,
                RequestCount = record.RequestCount,
                WindowStart = record.WindowStart,
                WindowEnd = record.WindowEnd,
                IsLimited = record.IsLimited,
                LimitTriggeredCount = record.LimitTriggeredCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取限流状态失败 - KeyType: {KeyType}, KeyValue: {KeyValue}", keyType, keyValue);
            return null;
        }
    }

    #region 私有方法

    private static DateTime GetWindowStart(DateTime now, int windowSeconds)
    {
        var ticks = now.Ticks / TimeSpan.TicksPerSecond / windowSeconds;
        return new DateTime(ticks * windowSeconds * TimeSpan.TicksPerSecond, DateTimeKind.Utc);
    }

    private static string GetCacheKey(string keyType, string keyValue, DateTime windowStart)
    {
        return $"{keyType}:{keyValue}:{windowStart:yyyyMMddHHmmss}";
    }

    private RateLimitRecord? GetFromCache(string key)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt > DateTime.UtcNow)
            {
                return entry.Record;
            }
            _cache.TryRemove(key, out _);
        }
        return null;
    }

    private void AddToCache(string key, RateLimitRecord record, DateTime expiresAt)
    {
        // 如果缓存过大，先清理一部分
        if (_cache.Count >= MaxCacheSize)
        {
            CleanupExpiredCache();
        }

        var entry = new RateLimitCacheEntry
        {
            Record = record,
            ExpiresAt = expiresAt
        };

        _cache[key] = entry;
    }

    private void CleanupExpiredCacheIfNeeded()
    {
        var now = DateTime.UtcNow;
        if (now - _lastCleanupTime > _cacheCleanupInterval)
        {
            CleanupExpiredCache();
            _lastCleanupTime = now;
        }
    }

    private int CleanupExpiredCache()
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _cache
            .Where(x => x.Value.ExpiresAt <= now)
            .Select(x => x.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }

        return expiredKeys.Count;
    }

    private async Task<RateLimitRecord> GetOrCreateFromDbAsync(
        string keyType,
        string keyValue,
        DateTime windowStart,
        DateTime windowEnd,
        DateTime now)
    {
        var existing = await _db.QueryFirstOrDefaultAsync<RateLimitRecord>(
            SecuritySql.GetRateLimit,
            new { KeyType = keyType, KeyValue = keyValue, WindowStart = windowStart });

        if (existing is not null)
        {
            return existing;
        }

        var record = new RateLimitRecord
        {
            Id = Guid.NewGuid(),
            KeyType = keyType,
            KeyValue = keyValue,
            RequestCount = 0,
            WindowStart = windowStart,
            WindowEnd = windowEnd,
            IsLimited = false,
            LimitTriggeredCount = 0,
            LastRequestAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _db.ExecuteAsync(SecuritySql.UpsertRateLimit, record);
        return record;
    }

    private async Task UpdateToDbAsync(RateLimitRecord record)
    {
        record.UpdatedAt = DateTime.UtcNow;
        await _db.ExecuteAsync(SecuritySql.UpsertRateLimit, record);
    }

    private async Task<int> GetSlidingWindowCountAsync(
        string keyType,
        string keyValue,
        DateTime windowStart,
        DateTime windowEnd)
    {
        const string sql = @"
            SELECT COALESCE(SUM(request_count), 0) 
            FROM security_rate_limits 
            WHERE key_type = @KeyType 
            AND key_value = @KeyValue 
            AND window_start >= @WindowStart 
            AND window_end <= @WindowEnd";

        return await _db.ExecuteScalarAsync<int>(sql, new
        {
            KeyType = keyType,
            KeyValue = keyValue,
            WindowStart = windowStart,
            WindowEnd = windowEnd
        });
    }

    #endregion

    /// <summary>
    /// 限流缓存条目
    /// </summary>
    private class RateLimitCacheEntry
    {
        public RateLimitRecord Record { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }
    }

    /// <inheritdoc />
    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            // 尝试查询一条记录验证数据库连接
            const string sql = "SELECT COUNT(*) FROM security_rate_limits LIMIT 1";
            await _db.ExecuteScalarAsync<int>(sql);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RateLimitService健康检查失败");
            return false;
        }
    }
}
