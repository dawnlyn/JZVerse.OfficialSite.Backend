using JZVerse.MicroHuaxia.ConfigCenter.Abstractions;
using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;
using Microsoft.AspNetCore.Mvc;

namespace JZVerse.MicroHuaxia.ConfigCenter.Server.Controllers;

/// <summary>
/// 灰度发布控制器
/// </summary>
[ApiController]
[Route("api/v1/gray-releases")]
public class GrayReleaseController(
    IGrayReleaseManager _releaseManager,
    ILogger<GrayReleaseController> _logger
) : ControllerBase
{
    /// <summary>
    /// 创建灰度发布
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(GrayRelease), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateRelease(
        [FromBody] CreateGrayReleaseRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.ReleaseName) ||
            string.IsNullOrEmpty(request.NamespaceId) ||
            string.IsNullOrEmpty(request.EnvironmentId))
        {
            return BadRequest("ReleaseName, NamespaceId and EnvironmentId are required");
        }

        var release = new GrayRelease
        {
            ReleaseId = string.Empty,
            ReleaseName = request.ReleaseName,
            NamespaceId = request.NamespaceId,
            EnvironmentId = request.EnvironmentId,
            Strategy = request.Strategy,
            TargetRules = request.Rules ?? [],
            RolloutPercentage = request.RolloutPercentage,
            CreatedBy = request.CreatedBy,
        };

        var result = await _releaseManager.CreateReleaseAsync(release, cancellationToken);

        _logger.LogInformation("Created gray release {ReleaseId} for namespace {NamespaceId}",
            result.ReleaseId, result.NamespaceId);

        return CreatedAtAction(nameof(GetRelease), new { releaseId = result.ReleaseId }, result);
    }

    /// <summary>
    /// 获取灰度发布详情
    /// </summary>
    [HttpGet("{releaseId}")]
    [ProducesResponseType(typeof(GrayRelease), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRelease(string releaseId, CancellationToken cancellationToken)
    {
        var release = await _releaseManager.GetReleaseAsync(releaseId, cancellationToken);
        if (release is null)
        {
            return NotFound();
        }
        return Ok(release);
    }

    /// <summary>
    /// 开始灰度发布
    /// </summary>
    [HttpPost("{releaseId}/start")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StartRelease(string releaseId, CancellationToken cancellationToken)
    {
        var result = await _releaseManager.StartReleaseAsync(releaseId, cancellationToken);
        if (!result)
        {
            return BadRequest("Cannot start release, check current status");
        }

        _logger.LogInformation("Started gray release {ReleaseId}", releaseId);
        return Ok(new { message = "Release started" });
    }

    /// <summary>
    /// 完成灰度发布
    /// </summary>
    [HttpPost("{releaseId}/complete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CompleteRelease(string releaseId, CancellationToken cancellationToken)
    {
        var result = await _releaseManager.CompleteReleaseAsync(releaseId, cancellationToken);
        if (!result)
        {
            return BadRequest("Cannot complete release, check current status");
        }

        _logger.LogInformation("Completed gray release {ReleaseId}", releaseId);
        return Ok(new { message = "Release completed" });
    }

    /// <summary>
    /// 回滚灰度发布
    /// </summary>
    [HttpPost("{releaseId}/rollback")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RollbackRelease(string releaseId, CancellationToken cancellationToken)
    {
        var result = await _releaseManager.RollbackReleaseAsync(releaseId, cancellationToken);
        if (!result)
        {
            return BadRequest("Cannot rollback release, check current status");
        }

        _logger.LogInformation("Rolled back gray release {ReleaseId}", releaseId);
        return Ok(new { message = "Release rolled back" });
    }

    /// <summary>
    /// 获取命名空间当前进行中的灰度发布
    /// </summary>
    [HttpGet("namespaces/{namespaceId}/active")]
    [ProducesResponseType(typeof(IReadOnlyList<GrayRelease>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveReleases(
        string namespaceId,
        [FromQuery] string environmentId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(environmentId))
        {
            return BadRequest("EnvironmentId is required");
        }

        var releases = await _releaseManager.GetActiveReleasesAsync(
            namespaceId, environmentId, cancellationToken);
        return Ok(releases);
    }

    /// <summary>
    /// 测试客户端是否匹配灰度规则
    /// </summary>
    [HttpPost("{releaseId}/match")]
    [ProducesResponseType(typeof(MatchResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TestMatch(
        string releaseId,
        [FromBody] ClientInfo clientInfo,
        CancellationToken cancellationToken)
    {
        var release = await _releaseManager.GetReleaseAsync(releaseId, cancellationToken);
        if (release is null)
        {
            return NotFound();
        }

        var matches = await _releaseManager.MatchesGrayRuleAsync(releaseId, clientInfo, cancellationToken);
        return Ok(new MatchResult { Matches = matches, ReleaseId = releaseId });
    }
}

/// <summary>
/// 创建灰度发布请求模型
/// </summary>
public sealed class CreateGrayReleaseRequest
{
    public string? ReleaseName { get; init; }
    public string? NamespaceId { get; init; }
    public string? EnvironmentId { get; init; }
    public GrayReleaseStrategy Strategy { get; init; }
    public List<GrayReleaseRule>? Rules { get; init; }
    public int RolloutPercentage { get; init; }
    public string? CreatedBy { get; init; }
}

/// <summary>
/// 匹配结果模型
/// </summary>
public sealed class MatchResult
{
    public bool Matches { get; init; }
    public required string ReleaseId { get; init; }
}
