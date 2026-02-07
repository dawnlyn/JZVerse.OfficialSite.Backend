using JZVerse.MicroHuaxia.ConfigCenter.Abstractions;
using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;
using Microsoft.AspNetCore.Mvc;

namespace JZVerse.MicroHuaxia.ConfigCenter.Server.Controllers;

/// <summary>
/// 配置发现控制器
/// </summary>
[ApiController]
[Route("api/v1/discovery")]
public class ConfigDiscoveryController(
    IConfigDiscovery _discovery,
    ILogger<ConfigDiscoveryController> _logger
) : ControllerBase
{
    /// <summary>
    /// 查询配置
    /// </summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IReadOnlyList<ConfigItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> QueryConfig(
        [FromBody] ConfigQuery query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(query.ApplicationId) || string.IsNullOrEmpty(query.EnvironmentId))
        {
            return BadRequest("ApplicationId and EnvironmentId are required");
        }

        var items = await _discovery.QueryAsync(query, cancellationToken);

        _logger.LogDebug("Query returned {Count} config items", items.Count);

        return Ok(items);
    }

    /// <summary>
    /// 获取命名空间配置（支持灰度）
    /// </summary>
    [HttpGet("applications/{applicationId}/environments/{environmentId}/namespaces/{namespaceId}")]
    [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNamespaceConfig(
        string applicationId,
        string environmentId,
        string namespaceId,
        [FromQuery] string? clientId,
        [FromQuery] string? clientIp,
        [FromQuery] string? tags,
        CancellationToken cancellationToken)
    {
        ClientInfo? clientInfo = null;
        if (!string.IsNullOrEmpty(clientId))
        {
            clientInfo = new ClientInfo
            {
                ClientId = clientId,
                IpAddress = clientIp,
                Tags = string.IsNullOrEmpty(tags)
                    ? []
                    : [.. tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
            };
        }

        var config = await _discovery.GetNamespaceConfigAsync(
            namespaceId, environmentId, clientInfo, cancellationToken);

        return Ok(config);
    }

    /// <summary>
    /// 获取单个配置值
    /// </summary>
    [HttpGet("value")]
    [ProducesResponseType(typeof(ConfigValueResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetValue(
        [FromQuery] string applicationId,
        [FromQuery] string environmentId,
        [FromQuery] string key,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(applicationId) ||
            string.IsNullOrEmpty(environmentId) ||
            string.IsNullOrEmpty(key))
        {
            return BadRequest("ApplicationId, EnvironmentId and Key are required");
        }

        var value = await _discovery.GetValueAsync(applicationId, environmentId, key, cancellationToken);

        if (value is null)
        {
            return NotFound();
        }

        return Ok(new ConfigValueResponse { Key = key, Value = value });
    }

    /// <summary>
    /// 获取完整配置（字典格式）
    /// </summary>
    [HttpPost("config")]
    [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConfig(
        [FromBody] ConfigQuery query,
        CancellationToken cancellationToken)
    {
        var config = await _discovery.GetConfigAsync(query, cancellationToken);
        return Ok(config);
    }
}

/// <summary>
/// 配置值响应模型
/// </summary>
public sealed class ConfigValueResponse
{
    public required string Key { get; init; }
    public required string Value { get; init; }
}
