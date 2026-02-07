using JZVerse.MicroHuaxia.ConfigCenter.Abstractions;
using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;
using Microsoft.AspNetCore.Mvc;

namespace JZVerse.MicroHuaxia.ConfigCenter.Server.Controllers;

/// <summary>
/// 配置管理控制器
/// </summary>
[ApiController]
[Route("api/v1/config")]
public class ConfigController(
    IConfigRegistry _registry,
    ILogger<ConfigController> _logger
) : ControllerBase
{
    /// <summary>
    /// 创建或更新配置项
    /// </summary>
    [HttpPost("items")]
    [ProducesResponseType(typeof(ConfigItem), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetConfigItem(
        [FromBody] ConfigItemRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Key) ||
            string.IsNullOrEmpty(request.NamespaceId) ||
            string.IsNullOrEmpty(request.EnvironmentId))
        {
            return BadRequest("Key, NamespaceId and EnvironmentId are required");
        }

        var item = new ConfigItem
        {
            ItemId = request.ItemId ?? string.Empty,
            Key = request.Key,
            Value = request.Value ?? string.Empty,
            ValueType = request.ValueType,
            NamespaceId = request.NamespaceId,
            EnvironmentId = request.EnvironmentId,
            Comment = request.Comment,
            IsSecret = request.IsSecret,
            IsRequired = request.IsRequired,
            UpdatedBy = request.UpdatedBy,
        };

        var result = await _registry.SetAsync(item, cancellationToken);

        _logger.LogInformation("Set config item {Key} in namespace {NamespaceId}",
            result.Key, result.NamespaceId);

        return CreatedAtAction(nameof(GetConfigItem), new { itemId = result.ItemId }, result);
    }

    /// <summary>
    /// 获取配置项详情
    /// </summary>
    [HttpGet("items/{itemId}")]
    [ProducesResponseType(typeof(ConfigItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConfigItem(string itemId, CancellationToken cancellationToken)
    {
        var item = await _registry.GetAsync(itemId, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }
        return Ok(item);
    }

    /// <summary>
    /// 删除配置项
    /// </summary>
    [HttpDelete("items/{itemId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteConfigItem(string itemId, CancellationToken cancellationToken)
    {
        var result = await _registry.DeleteAsync(itemId, cancellationToken);
        if (!result)
        {
            return NotFound();
        }
        return NoContent();
    }

    /// <summary>
    /// 获取命名空间下所有配置项
    /// </summary>
    [HttpGet("namespaces/{namespaceId}/items")]
    [ProducesResponseType(typeof(IReadOnlyList<ConfigItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNamespaceItems(
        string namespaceId,
        [FromQuery] string environmentId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(environmentId))
        {
            return BadRequest("EnvironmentId is required");
        }

        var items = await _registry.GetByNamespaceAsync(namespaceId, environmentId, cancellationToken);
        return Ok(items);
    }

    /// <summary>
    /// 批量设置配置项
    /// </summary>
    [HttpPost("items/batch")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BatchSetConfigItems(
        [FromBody] List<ConfigItemRequest> requests,
        CancellationToken cancellationToken)
    {
        if (requests is null or { Count: 0 })
        {
            return BadRequest("At least one config item is required");
        }

        var items = requests.Select(r => new ConfigItem
        {
            ItemId = r.ItemId ?? string.Empty,
            Key = r.Key ?? string.Empty,
            Value = r.Value ?? string.Empty,
            ValueType = r.ValueType,
            NamespaceId = r.NamespaceId ?? string.Empty,
            EnvironmentId = r.EnvironmentId ?? string.Empty,
            Comment = r.Comment,
            IsSecret = r.IsSecret,
            IsRequired = r.IsRequired,
            UpdatedBy = r.UpdatedBy,
        }).ToList();

        await _registry.BatchSetAsync(items, cancellationToken);

        _logger.LogInformation("Batch set {Count} config items", items.Count);

        return Ok(new { count = items.Count });
    }
}

/// <summary>
/// 配置项请求模型
/// </summary>
public sealed class ConfigItemRequest
{
    public string? ItemId { get; init; }
    public string? Key { get; init; }
    public string? Value { get; init; }
    public ConfigValueType ValueType { get; init; }
    public string? NamespaceId { get; init; }
    public string? EnvironmentId { get; init; }
    public string? Comment { get; init; }
    public bool IsSecret { get; init; }
    public bool IsRequired { get; init; }
    public string? UpdatedBy { get; init; }
}
