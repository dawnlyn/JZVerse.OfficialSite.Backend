using JZVerse.MicroHuaxia.ConfigCenter.Abstractions;
using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;
using Microsoft.AspNetCore.Mvc;

namespace JZVerse.MicroHuaxia.ConfigCenter.Server.Controllers;

/// <summary>
/// 配置版本控制器
/// </summary>
[ApiController]
[Route("api/v1/versions")]
public class ConfigVersionController(
    IConfigVersionManager _versionManager,
    ILogger<ConfigVersionController> _logger
) : ControllerBase
{
    /// <summary>
    /// 获取配置项版本历史
    /// </summary>
    [HttpGet("items/{itemId}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<ConfigVersion>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(
        string itemId,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var history = await _versionManager.GetHistoryAsync(itemId, limit, cancellationToken);
        return Ok(history);
    }

    /// <summary>
    /// 获取指定版本详情
    /// </summary>
    [HttpGet("items/{itemId}/versions/{version:long}")]
    [ProducesResponseType(typeof(ConfigVersion), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVersion(
        string itemId,
        long version,
        CancellationToken cancellationToken)
    {
        var versionRecord = await _versionManager.GetVersionAsync(itemId, version, cancellationToken);
        if (versionRecord is null)
        {
            return NotFound();
        }
        return Ok(versionRecord);
    }

    /// <summary>
    /// 回滚配置到指定版本
    /// </summary>
    [HttpPost("items/{itemId}/rollback")]
    [ProducesResponseType(typeof(ConfigItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Rollback(
        string itemId,
        [FromBody] RollbackRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _versionManager.RollbackAsync(
                itemId,
                request.TargetVersion,
                request.RollbackBy,
                cancellationToken);

            _logger.LogInformation(
                "Rolled back config item {ItemId} to version {TargetVersion}",
                itemId, request.TargetVersion);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Rollback failed for item {ItemId}", itemId);
            return NotFound(ex.Message);
        }
    }
}

/// <summary>
/// 回滚请求模型
/// </summary>
public sealed class RollbackRequest
{
    public long TargetVersion { get; init; }
    public string? Reason { get; init; }
    public string? RollbackBy { get; init; }
}
