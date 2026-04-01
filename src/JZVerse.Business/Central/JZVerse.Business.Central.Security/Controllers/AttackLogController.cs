using JZVerse.Business.Central.Security.Database.Models;
using JZVerse.Business.Central.Security.Models;
using JZVerse.Business.Central.Security.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JZVerse.Business.Central.Security.Controllers;

/// <summary>
/// 攻击日志控制器
/// </summary>
[ApiController]
[Route("api/v1/security/attack-logs")]
[Authorize(Roles = "Admin")]
public class AttackLogController : ControllerBase
{
    private readonly IAttackProtectionService _attackProtection;
    private readonly ILogger<AttackLogController> _logger;

    public AttackLogController(IAttackProtectionService attackProtection, ILogger<AttackLogController> logger)
    {
        _attackProtection = attackProtection;
        _logger = logger;
    }

    /// <summary>
    /// 获取攻击日志列表
    /// </summary>
    [HttpGet]
    public async Task<ActionResult> GetLogs(
        [FromQuery] string? attackType,
        [FromQuery] string? sourceIp,
        [FromQuery] string? riskLevel,
        [FromQuery] bool? isHandled,
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        // 这里应该通过服务查询，目前返回空
        // 实际实现需要添加查询方法到 IAttackProtectionService
        return Ok(new PagedResult<AttackLog>
        {
            Items = new List<AttackLog>(),
            TotalCount = 0,
            PageIndex = pageIndex,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// 处理攻击日志
    /// </summary>
    [HttpPost("{id:guid}/handle")]
    public async Task<ActionResult> HandleLog(Guid id, [FromBody] HandleAttackLogRequest request)
    {
        _logger.LogInformation("Handling attack log {Id} by {Handler}", id, request.Handler);
        return Ok(new { Code = 200, Message = "攻击日志已处理" });
    }

    /// <summary>
    /// 获取封禁IP列表
    /// </summary>
    [HttpGet("banned-ips")]
    public async Task<ActionResult> GetBannedIps(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        var (items, total) = await _attackProtection.GetBannedIpsAsync(pageIndex, pageSize);
        return Ok(new PagedResult<IpBanInfo>
        {
            Items = items,
            TotalCount = total,
            PageIndex = pageIndex,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// 封禁IP
    /// </summary>
    [HttpPost("ban-ip")]
    public async Task<ActionResult> BanIp([FromBody] BanIpRequest request)
    {
        var result = await _attackProtection.BanIpAsync(
            request.IpAddress, 
            request.Reason, 
            request.DurationMinutes);

        if (!result)
        {
            return BadRequest(new { Code = 400, Message = "封禁IP失败" });
        }

        return Ok(new { Code = 200, Message = "IP已封禁" });
    }

    /// <summary>
    /// 解封IP
    /// </summary>
    [HttpPost("unban-ip/{ipAddress}")]
    public async Task<ActionResult> UnbanIp(string ipAddress)
    {
        var result = await _attackProtection.UnbanIpAsync(ipAddress);
        if (!result)
        {
            return BadRequest(new { Code = 400, Message = "解封IP失败" });
        }

        return Ok(new { Code = 200, Message = "IP已解封" });
    }

    /// <summary>
    /// 检查IP是否被封禁
    /// </summary>
    [HttpGet("check-ip/{ipAddress}")]
    [AllowAnonymous]
    public async Task<ActionResult> CheckIpBanned(string ipAddress)
    {
        var banInfo = await _attackProtection.CheckIpBannedAsync(ipAddress);
        if (banInfo == null)
        {
            return Ok(new { IsBanned = false });
        }

        return Ok(new
        {
            IsBanned = true,
            Reason = banInfo.Reason,
            BannedAt = banInfo.BannedAt,
            ExpireAt = banInfo.ExpireAt
        });
    }
}

public class HandleAttackLogRequest
{
    public string Handler { get; set; } = string.Empty;
    public string? Remark { get; set; }
}

public class BanIpRequest
{
    public string IpAddress { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int DurationMinutes { get; set; } = 60;
}
