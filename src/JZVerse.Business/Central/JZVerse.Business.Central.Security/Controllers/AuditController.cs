using JZVerse.Business.Central.Security.Database.Models;
using JZVerse.Business.Central.Security.Models;
using JZVerse.Business.Central.Security.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JZVerse.Business.Central.Security.Controllers;

/// <summary>
/// 审计日志控制器
/// </summary>
[ApiController]
[Route("api/v1/security/audit")]
[Authorize(Roles = "Admin")]
public class AuditController : ControllerBase
{
    private readonly IAuditService _auditService;
    private readonly ILogger<AuditController> _logger;

    public AuditController(IAuditService auditService, ILogger<AuditController> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// 查询审计日志
    /// </summary>
    [HttpGet("logs")]
    public async Task<ActionResult<PagedResult<AuditLog>>> GetLogs(
        [FromQuery] Guid? userId,
        [FromQuery] string? operationType,
        [FromQuery] string? module,
        [FromQuery] string? result,
        [FromQuery] bool? isSensitive,
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new AuditLogQuery
        {
            UserId = userId,
            OperationType = operationType,
            Module = module,
            Result = result,
            IsSensitive = isSensitive,
            StartTime = startTime,
            EndTime = endTime,
            PageIndex = pageIndex,
            PageSize = pageSize
        };

        var (logs, total) = await _auditService.QueryAsync(query);

        return Ok(new PagedResult<AuditLog>
        {
            Items = logs,
            TotalCount = total,
            PageIndex = pageIndex,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// 获取审计日志详情
    /// </summary>
    [HttpGet("logs/{id:guid}")]
    public async Task<ActionResult<AuditLog>> GetLogDetail(Guid id)
    {
        var log = await _auditService.GetByIdAsync(id);
        if (log == null)
        {
            return NotFound(new { Code = 404, Message = "日志不存在" });
        }

        return Ok(log);
    }

    /// <summary>
    /// 获取用户的最近操作
    /// </summary>
    [HttpGet("logs/user/{userId:guid}/recent")]
    public async Task<ActionResult<List<AuditLog>>> GetUserRecentLogs(Guid userId, [FromQuery] int count = 10)
    {
        var logs = await _auditService.GetRecentByUserAsync(userId, count);
        return Ok(logs);
    }

    /// <summary>
    /// 获取操作类型统计
    /// </summary>
    [HttpGet("statistics/operation-types")]
    public async Task<ActionResult<Dictionary<string, int>>> GetOperationTypeStatistics(
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime)
    {
        var start = startTime ?? DateTime.UtcNow.AddDays(-7);
        var end = endTime ?? DateTime.UtcNow;

        var stats = await _auditService.StatisticsByOperationTypeAsync(start, end);
        return Ok(stats);
    }

    /// <summary>
    /// 获取模块统计
    /// </summary>
    [HttpGet("statistics/modules")]
    public async Task<ActionResult<Dictionary<string, int>>> GetModuleStatistics(
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime)
    {
        var start = startTime ?? DateTime.UtcNow.AddDays(-7);
        var end = endTime ?? DateTime.UtcNow;

        var stats = await _auditService.StatisticsByModuleAsync(start, end);
        return Ok(stats);
    }

    /// <summary>
    /// 清理过期审计日志
    /// </summary>
    [HttpDelete("logs/cleanup")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult> CleanOldLogs([FromQuery] DateTime beforeDate)
    {
        var count = await _auditService.CleanOldLogsAsync(beforeDate);
        return Ok(new { Code = 200, Message = $"已清理 {count} 条过期日志" });
    }
}
