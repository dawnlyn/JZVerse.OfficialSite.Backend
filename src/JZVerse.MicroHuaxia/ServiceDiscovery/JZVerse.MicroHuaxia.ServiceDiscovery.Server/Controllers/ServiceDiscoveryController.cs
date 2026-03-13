using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions;
using JZVerse.MicroHuaxia.ServiceDiscovery.Abstractions.Models;
using JZVerse.MicroHuaxia.ServiceDiscovery.Server.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Server.Controllers;

/// <summary>
/// 服务注册发现 API 控制器
/// </summary>
[ApiController]
[Route("api/v1/services")]
public class ServiceDiscoveryController(
    IServiceRegistry _registry,
    IServiceDiscovery _discovery,
    IHealthCheckManager _healthCheckManager,
    IOptions<ServiceDiscoveryServerOptions> _options,
    ILogger<ServiceDiscoveryController> _logger
) : ControllerBase
{
    /// <summary>
    /// 注册服务实例
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ServiceInstance), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(
        [FromBody] ServiceRegistration registration,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var instance = await _registry.RegisterAsync(registration, cancellationToken);
            return CreatedAtAction(nameof(GetInstanceById), new { instanceId = instance.InstanceId }, instance);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to register service: {ServiceName}", registration.ServiceName);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error registering service: {ServiceName}", registration.ServiceName);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// 注销服务实例
    /// </summary>
    [HttpDelete("{instanceId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deregister(string instanceId, CancellationToken cancellationToken)
    {
        var result = await _registry.DeregisterAsync(instanceId, cancellationToken);
        return result ? NoContent() : NotFound(new { error = $"Instance '{instanceId}' not found" });
    }

    /// <summary>
    /// 发送心跳
    /// </summary>
    [HttpPut("{instanceId}/heartbeat")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Heartbeat(string instanceId, CancellationToken cancellationToken)
    {
        var result = await _registry.HeartbeatAsync(instanceId, cancellationToken);
        return result
            ? Ok(new { message = "Heartbeat received" })
            : NotFound(new { error = $"Instance '{instanceId}' not found" });
    }

    /// <summary>
    /// 更新服务健康状态
    /// </summary>
    [HttpPut("{instanceId}/health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateHealth(
        string instanceId,
        [FromBody] UpdateHealthRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _registry.UpdateHealthStatusAsync(instanceId, request.Status, cancellationToken);
        return result
            ? Ok(new { message = "Health status updated" })
            : NotFound(new { error = $"Instance '{instanceId}' not found" });
    }

    /// <summary>
    /// 更新服务元数据
    /// </summary>
    [HttpPut("{instanceId}/metadata")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMetadata(
        string instanceId,
        [FromBody] ServiceMetadata metadata,
        CancellationToken cancellationToken
    )
    {
        var result = await _registry.UpdateMetadataAsync(instanceId, metadata, cancellationToken);
        return result
            ? Ok(new { message = "Metadata updated" })
            : NotFound(new { error = $"Instance '{instanceId}' not found" });
    }

    /// <summary>
    /// 获取所有服务名称
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetServiceNames(CancellationToken cancellationToken)
    {
        var names = await _discovery.GetServiceNamesAsync(cancellationToken);
        return Ok(names);
    }

    /// <summary>
    /// 获取指定服务的所有实例
    /// </summary>
    [HttpGet("{serviceName}")]
    [ProducesResponseType(typeof(IReadOnlyList<ServiceInstance>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInstances(
        string serviceName,
        [FromQuery] bool includeDeregistered = false,
        CancellationToken cancellationToken = default
    )
    {
        if (includeDeregistered)
        {
            var query = new ServiceQuery
            {
                ServiceName = serviceName,
                IncludeDeregistered = true,
                OnlyHealthy = false,
                OnlyEnabled = false,
            };
            var instances = await _discovery.DiscoverAsync(query, cancellationToken);
            return Ok(instances);
        }

        var result = await _discovery.GetInstancesAsync(serviceName, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// 获取单个服务实例（负载均衡）
    /// </summary>
    [HttpGet("{serviceName}/instance")]
    [ProducesResponseType(typeof(ServiceInstance), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInstance(string serviceName, CancellationToken cancellationToken)
    {
        var instance = await _discovery.GetInstanceAsync(serviceName, cancellationToken);
        return instance != null
            ? Ok(instance)
            : NotFound(new { error = $"No available instance for service '{serviceName}'" });
    }

    /// <summary>
    /// 根据条件查询服务
    /// </summary>
    [HttpPost("discover")]
    [ProducesResponseType(typeof(IReadOnlyList<ServiceInstance>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Discover([FromBody] ServiceQuery query, CancellationToken cancellationToken)
    {
        var instances = await _discovery.DiscoverAsync(query, cancellationToken);
        return Ok(instances);
    }

    /// <summary>
    /// 获取指定实例详情
    /// </summary>
    [HttpGet("/api/v1/instances/{instanceId}")]
    [ProducesResponseType(typeof(ServiceInstance), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInstanceById(string instanceId, CancellationToken cancellationToken)
    {
        var instance = await _discovery.GetInstanceByIdAsync(instanceId, cancellationToken);
        return instance != null ? Ok(instance) : NotFound(new { error = $"Instance '{instanceId}' not found" });
    }

    /// <summary>
    /// 永久删除已注销的服务实例
    /// </summary>
    [HttpDelete("{instanceId}/purge")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Purge(string instanceId, CancellationToken cancellationToken)
    {
        var result = await _registry.PurgeAsync(instanceId, cancellationToken);
        return result ? NoContent() : NotFound(new { error = $"Instance '{instanceId}' not found or not deregistered" });
    }

    /// <summary>
    /// 手动触发健康检查
    /// </summary>
    [HttpGet("/api/v1/instances/{instanceId}/health")]
    [ProducesResponseType(typeof(HealthCheckResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckInstanceHealth(string instanceId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _healthCheckManager.CheckInstanceAsync(instanceId, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// 获取服务发现配置
    /// </summary>
    [HttpGet("config")]
    [ProducesResponseType(typeof(ServiceDiscoveryServerOptions), StatusCodes.Status200OK)]
    public IActionResult GetConfig()
    {
        return Ok(_options.Value);
    }
}

/// <summary>
/// 更新健康状态请求
/// </summary>
public class UpdateHealthRequest
{
    /// <summary>
    /// 健康状态
    /// </summary>
    public HealthStatus Status { get; set; }
}
