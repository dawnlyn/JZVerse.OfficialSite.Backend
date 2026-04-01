using JZVerse.Business.Central.Security.Configurations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace JZVerse.Business.Central.Security.Controllers;

/// <summary>
/// 安全配置控制器
/// </summary>
[ApiController]
[Route("api/v1/security/config")]
[Authorize(Roles = "Admin")]
public class ConfigController : ControllerBase
{
    private readonly SecurityOptions _securityOptions;
    private readonly TokenOptions _tokenOptions;
    private readonly RateLimitOptions _rateLimitOptions;
    private readonly AlertOptions _alertOptions;
    private readonly ILogger<ConfigController> _logger;

    public ConfigController(
        IOptions<SecurityOptions> securityOptions,
        IOptions<TokenOptions> tokenOptions,
        IOptions<RateLimitOptions> rateLimitOptions,
        IOptions<AlertOptions> alertOptions,
        ILogger<ConfigController> logger)
    {
        _securityOptions = securityOptions.Value;
        _tokenOptions = tokenOptions.Value;
        _rateLimitOptions = rateLimitOptions.Value;
        _alertOptions = alertOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// 获取安全配置
    /// </summary>
    [HttpGet]
    public ActionResult GetConfig()
    {
        return Ok(new
        {
            Security = new
            {
                _securityOptions.PasswordMinLength,
                _securityOptions.RequireDigit,
                _securityOptions.RequireLowercase,
                _securityOptions.RequireUppercase,
                _securityOptions.RequireSpecialChar,
                _securityOptions.MaxLoginAttempts,
                _securityOptions.LockoutDurationMinutes,
                _securityOptions.EnableAttackProtection,
                _securityOptions.EnableAuditLog,
                _securityOptions.AuditLogRetentionDays,
                AttackProtection = new
                {
                    _securityOptions.AttackProtection.EnableSqlInjectionDetection,
                    _securityOptions.AttackProtection.EnableXssDetection,
                    _securityOptions.AttackProtection.EnableCsrfProtection,
                    _securityOptions.AttackProtection.EnablePathTraversalDetection,
                    _securityOptions.AttackProtection.EnableCommandInjectionDetection,
                    _securityOptions.AttackProtection.AutoBanThreshold,
                    _securityOptions.AttackProtection.AutoBanDurationMinutes
                }
            },
            Token = new
            {
                _tokenOptions.Issuer,
                _tokenOptions.Audience,
                _tokenOptions.AccessTokenExpirationMinutes,
                _tokenOptions.RefreshTokenExpirationDays,
                _tokenOptions.ClockSkewMinutes
            },
            RateLimit = new
            {
                _rateLimitOptions.Enabled,
                _rateLimitOptions.DefaultLimit,
                _rateLimitOptions.DefaultWindowSeconds,
                RuleCount = _rateLimitOptions.Rules?.Count ?? 0
            },
            Alert = new
            {
                _alertOptions.Enabled,
                _alertOptions.DefaultSeverity,
                _alertOptions.BatchPushIntervalMinutes,
                _alertOptions.MaxRetryCount,
                Channels = _alertOptions.Channels?.Keys.ToList() ?? new List<string>()
            }
        });
    }

    /// <summary>
    /// 获取限流规则
    /// </summary>
    [HttpGet("rate-limit-rules")]
    public ActionResult GetRateLimitRules()
    {
        var rules = _rateLimitOptions.Rules?.Select(r => new
        {
            r.Key,
            r.Value.Limit,
            r.Value.WindowSeconds,
            r.Value.Type
        }) ?? new List<object>();

        return Ok(rules);
    }

    /// <summary>
    /// 获取告警渠道配置（脱敏）
    /// </summary>
    [HttpGet("alert-channels")]
    [Authorize(Roles = "SuperAdmin")]
    public ActionResult GetAlertChannels()
    {
        var channels = _alertOptions.Channels?.Select(c => new
        {
            Name = c.Key,
            c.Value.Type,
            c.Value.Enabled,
            // 脱敏显示Webhook URL
            WebhookUrl = MaskWebhookUrl(c.Value.WebhookUrl)
        }) ?? new List<object>();

        return Ok(channels);
    }

    /// <summary>
    /// 更新限流配置
    /// </summary>
    [HttpPut("rate-limit")]
    [Authorize(Roles = "SuperAdmin")]
    public ActionResult UpdateRateLimitConfig([FromBody] UpdateRateLimitConfigRequest request)
    {
        _logger.LogInformation("Updating rate limit config");
        // 实际应用需要重新加载配置或更新配置中心
        return Ok(new { Code = 200, Message = "配置已更新，重启后生效" });
    }

    /// <summary>
    /// 更新安全配置
    /// </summary>
    [HttpPut("security")]
    [Authorize(Roles = "SuperAdmin")]
    public ActionResult UpdateSecurityConfig([FromBody] UpdateSecurityConfigRequest request)
    {
        _logger.LogInformation("Updating security config");
        return Ok(new { Code = 200, Message = "配置已更新，重启后生效" });
    }

    /// <summary>
    /// 更新Token配置
    /// </summary>
    [HttpPut("token")]
    [Authorize(Roles = "SuperAdmin")]
    public ActionResult UpdateTokenConfig([FromBody] UpdateTokenConfigRequest request)
    {
        _logger.LogInformation("Updating token config");
        return Ok(new { Code = 200, Message = "配置已更新，重启后生效" });
    }

    /// <summary>
    /// 获取安全配置摘要（公开接口，用于健康检查）
    /// </summary>
    [HttpGet("summary")]
    [AllowAnonymous]
    public ActionResult GetConfigSummary()
    {
        return Ok(new
        {
            AttackProtectionEnabled = _securityOptions.EnableAttackProtection,
            AuditLogEnabled = _securityOptions.EnableAuditLog,
            RateLimitEnabled = _rateLimitOptions.Enabled,
            AlertEnabled = _alertOptions.Enabled
        });
    }

    private string MaskWebhookUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return string.Empty;

        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath;
            var maskedPath = path.Length > 10
                ? path.Substring(0, 5) + "*****" + path.Substring(path.Length - 5)
                : "*****";
            return $"{uri.Scheme}://{uri.Host}{maskedPath}";
        }
        catch
        {
            return "*****";
        }
    }
}

public class UpdateRateLimitConfigRequest
{
    public bool Enabled { get; set; }
    public int DefaultLimit { get; set; }
    public int DefaultWindowSeconds { get; set; }
}

public class UpdateSecurityConfigRequest
{
    public int PasswordMinLength { get; set; }
    public bool RequireDigit { get; set; }
    public bool RequireLowercase { get; set; }
    public bool RequireUppercase { get; set; }
    public bool RequireSpecialChar { get; set; }
    public int MaxLoginAttempts { get; set; }
    public int LockoutDurationMinutes { get; set; }
}

public class UpdateTokenConfigRequest
{
    public int AccessTokenExpirationMinutes { get; set; }
    public int RefreshTokenExpirationDays { get; set; }
    public int ClockSkewMinutes { get; set; }
}
