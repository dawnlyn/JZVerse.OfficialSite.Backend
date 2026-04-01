using JZVerse.Business.Central.Security.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace JZVerse.Business.Central.Security.Controllers;

/// <summary>
/// 健康检查控制器
/// </summary>
[ApiController]
[Route("api/v1/security/health")]
public class HealthController : ControllerBase
{
    private readonly ITokenService _tokenService;
    private readonly IRateLimitService _rateLimitService;
    private readonly IAuditService _auditService;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        ITokenService tokenService,
        IRateLimitService rateLimitService,
        IAuditService auditService,
        ILogger<HealthController> logger)
    {
        _tokenService = tokenService;
        _rateLimitService = rateLimitService;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// 基础健康检查
    /// </summary>
    [HttpGet]
    public ActionResult Health()
    {
        return Ok(new
        {
            Status = "Healthy",
            Service = "SecurityCenter",
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// 详细健康检查
    /// </summary>
    [HttpGet("detailed")]
    public async Task<ActionResult> DetailedHealth()
    {
        var checks = new Dictionary<string, object>();
        var overallHealthy = true;

        // Token Service Check
        try
        {
            var tokenHealth = await _tokenService.HealthCheckAsync();
            checks["TokenService"] = new { Status = tokenHealth ? "Healthy" : "Unhealthy" };
            overallHealthy &= tokenHealth;
        }
        catch (Exception ex)
        {
            checks["TokenService"] = new { Status = "Unhealthy", Error = ex.Message };
            overallHealthy = false;
        }

        // RateLimit Service Check
        try
        {
            var rateLimitHealth = await _rateLimitService.HealthCheckAsync();
            checks["RateLimitService"] = new { Status = rateLimitHealth ? "Healthy" : "Unhealthy" };
            overallHealthy &= rateLimitHealth;
        }
        catch (Exception ex)
        {
            checks["RateLimitService"] = new { Status = "Unhealthy", Error = ex.Message };
            overallHealthy = false;
        }

        // Audit Service Check
        try
        {
            var auditHealth = await _auditService.HealthCheckAsync();
            checks["AuditService"] = new { Status = auditHealth ? "Healthy" : "Unhealthy" };
            overallHealthy &= auditHealth;
        }
        catch (Exception ex)
        {
            checks["AuditService"] = new { Status = "Unhealthy", Error = ex.Message };
            overallHealthy = false;
        }

        var response = new
        {
            Status = overallHealthy ? "Healthy" : "Unhealthy",
            Service = "SecurityCenter",
            Timestamp = DateTime.UtcNow,
            Checks = checks
        };

        if (!overallHealthy)
        {
            return StatusCode(503, response);
        }

        return Ok(response);
    }

    /// <summary>
    /// 服务状态信息
    /// </summary>
    [HttpGet("info")]
    public ActionResult Info()
    {
        return Ok(new
        {
            Service = "SecurityCenter",
            Version = "1.0.0",
            Description = "统一安全中心 - 提供认证、授权、审计、攻击防护等服务",
            Features = new[]
            {
                "Token Management",
                "Password Hashing (SM3/MD5+Salt)",
                "Rate Limiting",
                "Audit Logging",
                "Attack Protection (SQL Injection, XSS, CSRF)",
                "Security Alerting"
            },
            Timestamp = DateTime.UtcNow
        });
    }
}
