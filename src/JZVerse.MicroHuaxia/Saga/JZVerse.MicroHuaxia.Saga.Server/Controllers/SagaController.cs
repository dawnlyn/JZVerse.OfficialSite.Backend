using JZVerse.MicroHuaxia.Saga.Abstractions;
using JZVerse.MicroHuaxia.Saga.Abstractions.Models;
using JZVerse.MicroHuaxia.Saga.Server.Configuration;
using JZVerse.MicroHuaxia.Saga.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Saga.Server.Controllers;

/// <summary>
/// Saga 管理控制器
/// </summary>
[ApiController]
[Route("api/v1/saga")]
public class SagaController : ControllerBase
{
    private readonly ISagaOrchestrator _orchestrator;
    private readonly ISagaStore _sagaStore;
    private readonly IEnumerable<ISagaDefinition> _sagaDefinitions;
    private readonly IOptions<SagaServerOptions> _options;
    private readonly ILogger<SagaController> _logger;

    public SagaController(
        ISagaOrchestrator orchestrator,
        ISagaStore sagaStore,
        IEnumerable<ISagaDefinition> sagaDefinitions,
        IOptions<SagaServerOptions> options,
        ILogger<SagaController> logger)
    {
        _orchestrator = orchestrator;
        _sagaStore = sagaStore;
        _sagaDefinitions = sagaDefinitions;
        _options = options;
        _logger = logger;
    }

    // ==================== 实例管理 ====================

    /// <summary>
    /// 启动一个新的 Saga 实例
    /// </summary>
    [HttpPost("instances")]
    [ProducesResponseType(typeof(SagaInstance), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StartSaga([FromBody] SagaStartRequest request, CancellationToken cancellationToken)
    {
        var definition = _sagaDefinitions.FirstOrDefault(d => d.SagaId == request.SagaId);
        if (definition is null)
        {
            return NotFound(new { error = $"Saga definition '{request.SagaId}' not found" });
        }

        var context = new SagaContext();
        if (request.InitialData != null)
        {
            foreach (var (key, value) in request.InitialData)
            {
                context.Set(key, value);
            }
        }

        var instanceId = await _orchestrator.StartAsync(definition, context, cancellationToken);
        var instance = await _orchestrator.GetInstanceAsync(instanceId, cancellationToken);

        _logger.LogInformation("启动 Saga 实例: {InstanceId}, SagaId: {SagaId}", instanceId, request.SagaId);
        return CreatedAtAction(nameof(GetInstance), new { instanceId }, instance);
    }

    /// <summary>
    /// 查询 Saga 实例列表
    /// </summary>
    [HttpGet("instances")]
    [ProducesResponseType(typeof(IReadOnlyList<SagaInstance>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryInstances(
        [FromQuery] string? sagaId,
        [FromQuery] SagaStatus? status,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var instances = await _orchestrator.QueryInstancesAsync(sagaId, status, limit, cancellationToken);
        return Ok(instances);
    }

    /// <summary>
    /// 获取 Saga 实例详情
    /// </summary>
    [HttpGet("instances/{instanceId}")]
    [ProducesResponseType(typeof(SagaInstance), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInstance(string instanceId, CancellationToken cancellationToken)
    {
        var instance = await _orchestrator.GetInstanceAsync(instanceId, cancellationToken);
        if (instance is null)
        {
            return NotFound();
        }

        return Ok(instance);
    }

    /// <summary>
    /// 手动触发 Saga 补偿
    /// </summary>
    [HttpPost("instances/{instanceId}/compensate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Compensate(string instanceId, CancellationToken cancellationToken)
    {
        var instance = await _orchestrator.GetInstanceAsync(instanceId, cancellationToken);
        if (instance is null)
        {
            return NotFound();
        }

        await _orchestrator.CompensateAsync(instanceId, cancellationToken);
        _logger.LogInformation("触发 Saga 补偿: {InstanceId}", instanceId);
        return Ok(new { message = "Compensation triggered" });
    }

    /// <summary>
    /// 取消 Saga
    /// </summary>
    [HttpPost("instances/{instanceId}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(string instanceId, CancellationToken cancellationToken)
    {
        var instance = await _orchestrator.GetInstanceAsync(instanceId, cancellationToken);
        if (instance is null)
        {
            return NotFound();
        }

        var result = await _orchestrator.CancelAsync(instanceId, cancellationToken);
        _logger.LogInformation("取消 Saga: {InstanceId}, 结果: {Result}", instanceId, result);
        return Ok(new { success = result });
    }

    /// <summary>
    /// 恢复 Saga 执行
    /// </summary>
    [HttpPost("instances/{instanceId}/resume")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Resume(string instanceId, CancellationToken cancellationToken)
    {
        var instance = await _orchestrator.GetInstanceAsync(instanceId, cancellationToken);
        if (instance is null)
        {
            return NotFound();
        }

        var result = await _orchestrator.ResumeAsync(instanceId, cancellationToken);
        _logger.LogInformation("恢复 Saga: {InstanceId}, 结果: {Result}", instanceId, result);
        return Ok(new { success = result });
    }

    // ==================== 统计信息 ====================

    /// <summary>
    /// 获取 Saga 统计信息
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var instances = await _orchestrator.QueryInstancesAsync(limit: 10000, cancellationToken: cancellationToken);
        var stats = new
        {
            TotalInstances = instances.Count,
            Executing = instances.Count(i => i.Status == SagaStatus.Executing),
            Completed = instances.Count(i => i.Status == SagaStatus.Completed),
            Failed = instances.Count(i => i.Status == SagaStatus.Failed),
            Compensating = instances.Count(i => i.Status == SagaStatus.Compensating),
            Compensated = instances.Count(i => i.Status == SagaStatus.Compensated),
            TimedOut = instances.Count(i => i.Status == SagaStatus.TimedOut)
        };
        return Ok(stats);
    }

    // ==================== 配置查询 ====================

    /// <summary>
    /// 获取 Saga 服务配置
    /// </summary>
    [HttpGet("config")]
    [ProducesResponseType(typeof(SagaServerOptions), StatusCodes.Status200OK)]
    public IActionResult GetConfig()
    {
        return Ok(_options.Value);
    }

    // ==================== 健康检查 ====================

    /// <summary>
    /// 健康检查
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTimeOffset.UtcNow,
            definitions = _sagaDefinitions.Count()
        });
    }
}
