using JZVerse.MicroHuaxia.Security.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JZVerse.MicroHuaxia.Security.Server.Controllers;

/// <summary>
/// 授权控制器
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class AuthorizationController : ControllerBase
{
    private readonly IAuthorizationEngine _authEngine;
    private readonly ILogger<AuthorizationController> _logger;

    public AuthorizationController(
        IAuthorizationEngine authEngine,
        ILogger<AuthorizationController> logger)
    {
        _authEngine = authEngine;
        _logger = logger;
    }

    /// <summary>
    /// 评估访问请求
    /// </summary>
    [HttpPost("evaluate")]
    public async Task<IActionResult> Evaluate([FromBody] EvaluateRequest request)
    {
        var accessRequest = new AccessRequest
        {
            Subject = request.Subject,
            Resource = request.Resource,
            Action = request.Action,
            Namespace = request.Namespace,
            Context = request.Context ?? new Dictionary<string, object>(),
            RequestTime = DateTimeOffset.UtcNow
        };

        var decision = await _authEngine.EvaluateAsync(accessRequest);

        return Ok(new
        {
            allowed = decision.Allowed,
            reason = decision.Reason,
            matchedPolicies = decision.MatchedPolicies,
            elapsedMs = decision.ElapsedMilliseconds
        });
    }

    /// <summary>
    /// 批量评估访问请求
    /// </summary>
    [HttpPost("evaluate-batch")]
    public async Task<IActionResult> EvaluateBatch([FromBody] List<EvaluateRequest> requests)
    {
        var accessRequests = requests.Select(r => new AccessRequest
        {
            Subject = r.Subject,
            Resource = r.Resource,
            Action = r.Action,
            Namespace = r.Namespace,
            Context = r.Context ?? new Dictionary<string, object>()
        }).ToList();

        var decisions = await _authEngine.EvaluateBatchAsync(accessRequests);

        return Ok(decisions.Select(d => new
        {
            allowed = d.Allowed,
            reason = d.Reason
        }));
    }

    /// <summary>
    /// 检查是否允许访问
    /// </summary>
    [HttpPost("check")]
    public async Task<IActionResult> Check([FromBody] CheckRequest request)
    {
        var allowed = await _authEngine.IsAllowedAsync(
            request.Subject,
            request.Resource,
            request.Action,
            request.Context ?? new Dictionary<string, object>());

        return Ok(new { allowed });
    }
}

/// <summary>
/// 评估请求
/// </summary>
public class EvaluateRequest
{
    /// <summary>
    /// 请求主体
    /// </summary>
    public SecurityIdentity Subject { get; set; } = null!;

    /// <summary>
    /// 目标资源
    /// </summary>
    public string Resource { get; set; } = null!;

    /// <summary>
    /// 操作类型
    /// </summary>
    public string Action { get; set; } = null!;

    /// <summary>
    /// 命名空间
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// 上下文信息
    /// </summary>
    public Dictionary<string, object>? Context { get; set; }
}

/// <summary>
/// 快速检查请求
/// </summary>
public class CheckRequest
{
    /// <summary>
    /// 请求主体
    /// </summary>
    public SecurityIdentity Subject { get; set; } = null!;

    /// <summary>
    /// 目标资源
    /// </summary>
    public string Resource { get; set; } = null!;

    /// <summary>
    /// 操作类型
    /// </summary>
    public string Action { get; set; } = null!;

    /// <summary>
    /// 上下文信息
    /// </summary>
    public Dictionary<string, object>? Context { get; set; }
}
