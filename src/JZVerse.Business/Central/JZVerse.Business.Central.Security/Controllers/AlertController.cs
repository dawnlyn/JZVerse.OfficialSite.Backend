using JZVerse.Business.Central.Security.Database.Models;
using JZVerse.Business.Central.Security.Models;
using JZVerse.Business.Central.Security.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JZVerse.Business.Central.Security.Controllers;

/// <summary>
/// 安全告警控制器
/// </summary>
[ApiController]
[Route("api/v1/security/alerts")]
[Authorize(Roles = "Admin")]
public class AlertController : ControllerBase
{
    private readonly IAlertService _alertService;
    private readonly ILogger<AlertController> _logger;

    public AlertController(IAlertService alertService, ILogger<AlertController> logger)
    {
        _alertService = alertService;
        _logger = logger;
    }

    /// <summary>
    /// 获取告警列表
    /// </summary>
    [HttpGet]
    public async Task<ActionResult> GetAlerts(
        [FromQuery] string? alertType,
        [FromQuery] string? severity,
        [FromQuery] string? status,
        [FromQuery] Guid? userId,
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        var (items, total) = await _alertService.GetAlertsAsync(
            alertType, severity, status, userId, startTime, endTime, pageIndex, pageSize);

        return Ok(new PagedResult<SecurityAlert>
        {
            Items = items,
            TotalCount = total,
            PageIndex = pageIndex,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// 获取待处理告警
    /// </summary>
    [HttpGet("pending")]
    public async Task<ActionResult<List<SecurityAlert>>> GetPendingAlerts([FromQuery] int count = 20)
    {
        var alerts = await _alertService.GetPendingAlertsAsync(count);
        return Ok(alerts);
    }

    /// <summary>
    /// 获取告警详情
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SecurityAlert>> GetAlertDetail(Guid id)
    {
        var alert = await _alertService.GetAlertByIdAsync(id);
        if (alert == null)
        {
            return NotFound(new { Code = 404, Message = "告警不存在" });
        }

        return Ok(alert);
    }

    /// <summary>
    /// 处理告警
    /// </summary>
    [HttpPost("{id:guid}/process")]
    public async Task<ActionResult> ProcessAlert(Guid id, [FromBody] ProcessAlertRequest request)
    {
        var result = await _alertService.ProcessAlertAsync(id, request.Processor, request.Action, request.Remark);
        if (!result)
        {
            return BadRequest(new { Code = 400, Message = "处理告警失败" });
        }

        return Ok(new { Code = 200, Message = "告警已处理" });
    }

    /// <summary>
    /// 忽略告警
    /// </summary>
    [HttpPost("{id:guid}/ignore")]
    public async Task<ActionResult> IgnoreAlert(Guid id, [FromBody] string reason)
    {
        var result = await _alertService.IgnoreAlertAsync(id, reason);
        if (!result)
        {
            return BadRequest(new { Code = 400, Message = "忽略告警失败" });
        }

        return Ok(new { Code = 200, Message = "告警已忽略" });
    }

    /// <summary>
    /// 获取告警规则
    /// </summary>
    [HttpGet("rules")]
    public async Task<ActionResult<List<AlertRule>>> GetAlertRules()
    {
        var rules = await _alertService.GetAlertRulesAsync();
        return Ok(rules);
    }

    /// <summary>
    /// 更新告警规则
    /// </summary>
    [HttpPut("rules/{ruleId}")]
    public async Task<ActionResult> UpdateAlertRule(string ruleId, [FromBody] UpdateAlertRuleRequest request)
    {
        var result = await _alertService.UpdateAlertRuleAsync(ruleId, request);
        if (!result)
        {
            return BadRequest(new { Code = 400, Message = "更新规则失败" });
        }

        return Ok(new { Code = 200, Message = "规则已更新" });
    }

    /// <summary>
    /// 手动触发测试告警
    /// </summary>
    [HttpPost("test")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult> SendTestAlert([FromBody] TestAlertRequest request)
    {
        _logger.LogInformation("Sending test alert: {Title}", request.Title);

        var alert = new SecurityAlert
        {
            Id = Guid.NewGuid(),
            AlertType = "Test",
            Severity = request.Severity,
            Title = request.Title,
            Content = request.Content,
            Module = "Test",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        await _alertService.ProcessAndPushAlertAsync(alert);

        return Ok(new { Code = 200, Message = "测试告警已发送", AlertId = alert.Id });
    }

    /// <summary>
    /// 获取告警统计
    /// </summary>
    [HttpGet("statistics")]
    public async Task<ActionResult> GetStatistics(
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime)
    {
        var start = startTime ?? DateTime.UtcNow.AddDays(-7);
        var end = endTime ?? DateTime.UtcNow;

        var stats = await _alertService.GetStatisticsAsync(start, end);
        return Ok(stats);
    }
}

public class ProcessAlertRequest
{
    public string Processor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Remark { get; set; }
}

public class UpdateAlertRuleRequest
{
    public int Threshold { get; set; }
    public int TimeWindowMinutes { get; set; }
    public int CooldownMinutes { get; set; }
    public bool IsEnabled { get; set; }
}

public class TestAlertRequest
{
    public string Severity { get; set; } = "Info";
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
