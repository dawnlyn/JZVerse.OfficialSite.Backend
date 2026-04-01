using System.Collections.Concurrent;
using System.Text.Json;
using JZVerse.Business.Central.Security.Database;
using JZVerse.Business.Central.Security.Database.Models;
using JZVerse.Business.Central.Security.Services.Interfaces;
using JZVerse.MicroHuaxia.DataAccess.Abstractions;
using Microsoft.Extensions.Logging;

namespace JZVerse.Business.Central.Security.Services.Implementations;

/// <summary>
/// 审计服务实现
/// </summary>
public sealed class AuditService : IAuditService
{
    private readonly IDbExecutor _db;
    private readonly ILogger<AuditService> _logger;

    // 异步写入队列（预留MessageQueue接口）
    private readonly ConcurrentQueue<AuditLogEntry> _pendingQueue = new();
    private readonly int _maxQueueSize = 10000;

    public AuditService(IDbExecutor db, ILogger<AuditService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> LogAsync(AuditLogEntry entry)
    {
        try
        {
            // 验证必填字段
            if (string.IsNullOrWhiteSpace(entry.OperationType))
            {
                _logger.LogWarning("审计日志操作类型不能为空");
                return false;
            }

            if (string.IsNullOrWhiteSpace(entry.Module))
            {
                _logger.LogWarning("审计日志模块不能为空");
                return false;
            }

            if (string.IsNullOrWhiteSpace(entry.OperationContent))
            {
                _logger.LogWarning("审计日志操作内容不能为空");
                return false;
            }

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = entry.UserId,
                Username = entry.Username,
                OperationType = entry.OperationType,
                Module = entry.Module,
                OperationContent = entry.OperationContent,
                RequestData = entry.RequestData is null ? null : JsonSerializer.Serialize(entry.RequestData),
                ResponseData = entry.ResponseData is null ? null : JsonSerializer.Serialize(entry.ResponseData),
                Result = entry.Result,
                ErrorMessage = entry.ErrorMessage,
                IpAddress = entry.IpAddress,
                UserAgent = entry.UserAgent,
                RequestPath = entry.RequestPath,
                HttpMethod = entry.HttpMethod,
                DurationMs = entry.DurationMs,
                IsSensitive = entry.IsSensitive,
                CreatedAt = DateTime.UtcNow
            };

            // 直接写入数据库（后续可改为异步队列处理）
            await _db.ExecuteAsync(SecuritySql.InsertAuditLog, auditLog);

            _logger.LogDebug(
                "审计日志记录成功 - Id: {Id}, Operation: {Operation}, Module: {Module}",
                auditLog.Id, entry.OperationType, entry.Module);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "审计日志记录失败 - Operation: {Operation}", entry.OperationType);
            return false;
        }
    }

    /// <inheritdoc />
    public Task<bool> LogAsync(
        Guid? userId,
        string? username,
        string operationType,
        string module,
        string content,
        bool isSensitive = false)
    {
        var entry = new AuditLogEntry
        {
            UserId = userId,
            Username = username,
            OperationType = operationType,
            Module = module,
            OperationContent = content,
            IsSensitive = isSensitive,
            IpAddress = "",
            RequestPath = "",
            HttpMethod = "",
            Result = "Success"
        };

        return LogAsync(entry);
    }

    /// <inheritdoc />
    public async Task<(List<AuditLog> Logs, int Total)> QueryAsync(AuditLogQuery query)
    {
        try
        {
            var offset = (query.PageIndex - 1) * query.PageSize;

            var parameters = new
            {
                UserId = query.UserId,
                OperationType = query.OperationType,
                Module = query.Module,
                Result = query.Result,
                StartTime = query.StartTime,
                EndTime = query.EndTime,
                IsSensitive = query.IsSensitive,
                Limit = query.PageSize,
                Offset = offset
            };

            // 并行执行查询和计数
            var logsTask = _db.QueryAsync<AuditLog>(SecuritySql.QueryAuditLogs, parameters);
            var countTask = _db.ExecuteScalarAsync<int>(SecuritySql.CountAuditLogs, parameters);

            await Task.WhenAll(logsTask, countTask);

            var logs = logsTask.Result.ToList();
            var total = countTask.Result;

            _logger.LogDebug(
                "审计日志查询完成 - 总数: {Total}, 返回: {Count}",
                total, logs.Count);

            return (logs, total);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "审计日志查询失败");
            return (new List<AuditLog>(), 0);
        }
    }

    /// <inheritdoc />
    public async Task<AuditLog?> GetByIdAsync(Guid id)
    {
        try
        {
            const string sql = "SELECT * FROM security_audit_logs WHERE id = @Id LIMIT 1";
            return await _db.QueryFirstOrDefaultAsync<AuditLog>(sql, new { Id = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取审计日志详情失败 - Id: {Id}", id);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<List<AuditLog>> GetRecentByUserAsync(Guid userId, int count = 10)
    {
        try
        {
            const string sql = @"
                SELECT * FROM security_audit_logs 
                WHERE user_id = @UserId
                ORDER BY created_at DESC
                LIMIT @Count";

            var logs = await _db.QueryAsync<AuditLog>(sql, new { UserId = userId, Count = count });
            return logs.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户最近操作日志失败 - UserId: {UserId}", userId);
            return new List<AuditLog>();
        }
    }

    /// <inheritdoc />
    public async Task<int> CleanOldLogsAsync(DateTime beforeDate)
    {
        try
        {
            const string sql = "DELETE FROM security_audit_logs WHERE created_at < @BeforeDate";
            var affected = await _db.ExecuteAsync(sql, new { BeforeDate = beforeDate });

            _logger.LogInformation(
                "清理过期审计日志完成 - 截止日期: {BeforeDate}, 删除数量: {Count}",
                beforeDate, affected);

            return affected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理过期审计日志失败 - BeforeDate: {BeforeDate}", beforeDate);
            return 0;
        }
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, int>> StatisticsByOperationTypeAsync(DateTime startTime, DateTime endTime)
    {
        try
        {
            const string sql = @"
                SELECT operation_type AS OperationType, COUNT(*) AS Count
                FROM security_audit_logs
                WHERE created_at >= @StartTime AND created_at <= @EndTime
                GROUP BY operation_type
                ORDER BY Count DESC";

            var results = await _db.QueryAsync<OperationTypeStat>(sql, new
            {
                StartTime = startTime,
                EndTime = endTime
            });

            return results.ToDictionary(
                x => x.OperationType,
                x => x.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "统计操作类型分布失败");
            return new Dictionary<string, int>();
        }
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, int>> StatisticsByModuleAsync(DateTime startTime, DateTime endTime)
    {
        try
        {
            const string sql = @"
                SELECT module AS Module, COUNT(*) AS Count
                FROM security_audit_logs
                WHERE created_at >= @StartTime AND created_at <= @EndTime
                GROUP BY module
                ORDER BY Count DESC";

            var results = await _db.QueryAsync<ModuleStat>(sql, new
            {
                StartTime = startTime,
                EndTime = endTime
            });

            return results.ToDictionary(
                x => x.Module,
                x => x.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "统计模块分布失败");
            return new Dictionary<string, int>();
        }
    }

    #region MessageQueue 异步写入预留接口

    /// <summary>
    /// 将日志条目加入异步写入队列（预留）
    /// </summary>
    /// <param name="entry">审计日志条目</param>
    /// <returns>是否成功加入队列</returns>
    public bool EnqueueForAsyncProcessing(AuditLogEntry entry)
    {
        if (_pendingQueue.Count >= _maxQueueSize)
        {
            _logger.LogWarning("审计日志队列已满，丢弃新日志");
            return false;
        }

        _pendingQueue.Enqueue(entry);
        return true;
    }

    /// <summary>
    /// 批量处理队列中的日志（由后台服务调用）
    /// </summary>
    /// <param name="batchSize">批处理大小</param>
    /// <returns>处理数量</returns>
    public async Task<int> ProcessQueueAsync(int batchSize = 100)
    {
        var processed = 0;
        var batch = new List<AuditLogEntry>();

        while (processed < batchSize && _pendingQueue.TryDequeue(out var entry))
        {
            batch.Add(entry);
            processed++;
        }

        if (batch.Count == 0)
        {
            return 0;
        }

        try
        {
            // 批量插入
            foreach (var entry in batch)
            {
                await LogAsync(entry);
            }

            _logger.LogDebug("批量处理审计日志完成 - 数量: {Count}", batch.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量处理审计日志失败");
        }

        return processed;
    }

    /// <summary>
    /// 获取队列待处理数量
    /// </summary>
    public int GetPendingCount()
    {
        return _pendingQueue.Count;
    }

    #endregion

    #region 统计查询结果模型

    private class OperationTypeStat
    {
        public string OperationType { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    private class ModuleStat
    {
        public string Module { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    #endregion

    /// <inheritdoc />
    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            // 尝试查询一条记录验证数据库连接
            const string sql = "SELECT COUNT(*) FROM security_audit_logs LIMIT 1";
            await _db.ExecuteScalarAsync<int>(sql);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AuditService健康检查失败");
            return false;
        }
    }
}
