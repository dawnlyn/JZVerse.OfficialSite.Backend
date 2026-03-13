using JZVerse.MicroHuaxia.ConfigCenter.Abstractions;
using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;
using JZVerse.MicroHuaxia.ConfigCenter.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace JZVerse.MicroHuaxia.ConfigCenter.Server.Controllers;

/// <summary>
/// 配置监听控制器（Long Polling）
/// </summary>
[ApiController]
[Route("api/v1/discovery")]
public class ConfigWatchController(
    ConfigLongPollManager _pollManager,
    IConfigDiscovery _discovery,
    ILogger<ConfigWatchController> _logger
) : ControllerBase
{
    /// <summary>
    /// 长轮询等待配置变更
    /// </summary>
    /// <param name="namespaceId">命名空间 ID</param>
    /// <param name="environmentId">环境 ID</param>
    /// <param name="lastVersion">客户端上次拿到的版本号</param>
    /// <param name="timeout">最长等待秒数（默认 30，最大 120）</param>
    /// <param name="clientId">客户端 ID（可选，用于灰度）</param>
    /// <param name="tags">客户端标签（可选，逗号分隔）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [HttpGet("watch")]
    [ProducesResponseType(typeof(ConfigWatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Watch(
        [FromQuery] string namespaceId,
        [FromQuery] string environmentId,
        [FromQuery] long lastVersion = 0,
        [FromQuery] int timeout = 30,
        [FromQuery] string? clientId = null,
        [FromQuery] string? tags = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(namespaceId) || string.IsNullOrEmpty(environmentId))
        {
            return BadRequest("namespaceId and environmentId are required");
        }

        timeout = Math.Clamp(timeout, 1, 120);

        _logger.LogDebug(
            "Watch request: ns={NamespaceId}, env={EnvironmentId}, lastVer={LastVersion}, timeout={Timeout}s",
            namespaceId, environmentId, lastVersion, timeout);

        var hasChanged = await _pollManager.WaitForChangeAsync(
            namespaceId, environmentId, lastVersion, timeout, cancellationToken);

        var currentVersion = _pollManager.GetVersion(namespaceId, environmentId);

        if (!hasChanged)
        {
            // 超时，无变更
            return Ok(new ConfigWatchResponse
            {
                HasChanged = false,
                Version = currentVersion,
            });
        }

        // 有变更，返回最新配置
        ClientInfo? clientInfo = null;
        if (!string.IsNullOrEmpty(clientId))
        {
            clientInfo = new ClientInfo
            {
                ClientId = clientId,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                Tags = string.IsNullOrEmpty(tags)
                    ? []
                    : [.. tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
            };
        }

        var config = await _discovery.GetNamespaceConfigAsync(
            namespaceId, environmentId, clientInfo, cancellationToken);

        return Ok(new ConfigWatchResponse
        {
            HasChanged = true,
            Version = currentVersion,
            Config = config,
            Timestamp = DateTimeOffset.UtcNow,
        });
    }
}

/// <summary>
/// 配置监听响应
/// </summary>
public sealed class ConfigWatchResponse
{
    /// <summary>
    /// 是否有配置变更
    /// </summary>
    public bool HasChanged { get; init; }

    /// <summary>
    /// 当前服务器版本号
    /// </summary>
    public long Version { get; init; }

    /// <summary>
    /// 最新配置（仅在 HasChanged=true 时有值）
    /// </summary>
    public Dictionary<string, string>? Config { get; init; }

    /// <summary>
    /// 服务器时间戳
    /// </summary>
    public DateTimeOffset? Timestamp { get; init; }
}
