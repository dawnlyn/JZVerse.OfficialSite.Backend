using JZVerse.MicroHuaxia.ProcessManager.Models;
using JZVerse.MicroHuaxia.ProcessManager.Services;
using Microsoft.AspNetCore.Mvc;

namespace JZVerse.MicroHuaxia.ProcessManager.Controllers;

/// <summary>
/// 进程管理 API
/// </summary>
[ApiController]
[Route("api/v1/processes")]
public class ProcessController(
    ProcessManagerService _manager,
    ILogger<ProcessController> _logger
) : ControllerBase
{
    // ────────────────── 进程定义管理 ──────────────────

    /// <summary>
    /// 获取所有进程定义（启动配置）
    /// </summary>
    [HttpGet("definitions")]
    public IActionResult GetAllDefinitions()
    {
        return Ok(_manager.GetAllDefinitions());
    }

    /// <summary>
    /// 获取单个进程定义
    /// </summary>
    [HttpGet("{processId}/definition")]
    public IActionResult GetDefinition(string processId)
    {
        var def = _manager.GetDefinition(processId);
        return def is not null ? Ok(def) : NotFound();
    }

    /// <summary>
    /// 注册或更新进程定义（其他服务调用此接口告知 ProcessManager 自己的启动配置）
    /// </summary>
    [HttpPut("{processId}/definition")]
    public IActionResult RegisterOrUpdateDefinition(string processId, [FromBody] ManagedProcessDefinition definition)
    {
        definition.Id = processId;
        _manager.RegisterOrUpdate(definition);
        return Ok(definition);
    }

    /// <summary>
    /// 注销进程定义（进程必须已停止）
    /// </summary>
    [HttpDelete("{processId}/definition")]
    public IActionResult UnregisterDefinition(string processId)
    {
        return _manager.Unregister(processId) ? Ok() : BadRequest("进程正在运行，无法注销");
    }

    // ────────────────── 状态查询 ──────────────────

    /// <summary>
    /// 获取所有进程状态
    /// </summary>
    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(_manager.GetAllStatus());
    }

    /// <summary>
    /// 获取单个进程状态
    /// </summary>
    [HttpGet("{processId}")]
    public IActionResult Get(string processId)
    {
        var status = _manager.GetStatus(processId);
        return status is not null ? Ok(status) : NotFound();
    }

    // ────────────────── 启停操作 ──────────────────

    /// <summary>
    /// 启动进程
    /// </summary>
    [HttpPost("{processId}/start")]
    public async Task<IActionResult> Start(string processId, CancellationToken ct)
    {
        _logger.LogInformation("Start requested for process {Id}", processId);
        var result = await _manager.StartAsync(processId, ct);
        return result ? Ok(_manager.GetStatus(processId)) : NotFound();
    }

    /// <summary>
    /// 停止进程
    /// </summary>
    [HttpPost("{processId}/stop")]
    public async Task<IActionResult> Stop(string processId, CancellationToken ct)
    {
        _logger.LogInformation("Stop requested for process {Id}", processId);
        var result = await _manager.StopAsync(processId, ct);
        return result ? Ok(_manager.GetStatus(processId)) : NotFound();
    }

    /// <summary>
    /// 重启进程
    /// </summary>
    [HttpPost("{processId}/restart")]
    public async Task<IActionResult> Restart(string processId, CancellationToken ct)
    {
        _logger.LogInformation("Restart requested for process {Id}", processId);
        var result = await _manager.RestartAsync(processId, ct);
        return result ? Ok(_manager.GetStatus(processId)) : NotFound();
    }

    /// <summary>
    /// 启动所有进程
    /// </summary>
    [HttpPost("start-all")]
    public async Task<IActionResult> StartAll(CancellationToken ct)
    {
        _logger.LogInformation("Start all processes requested");
        await _manager.StartAllAsync(ct);
        return Ok(_manager.GetAllStatus());
    }

    /// <summary>
    /// 停止所有进程
    /// </summary>
    [HttpPost("stop-all")]
    public async Task<IActionResult> StopAll(CancellationToken ct)
    {
        _logger.LogInformation("Stop all processes requested");
        await _manager.StopAllAsync(ct);
        return Ok(_manager.GetAllStatus());
    }

    /// <summary>
    /// 重启所有进程
    /// </summary>
    [HttpPost("restart-all")]
    public async Task<IActionResult> RestartAll(CancellationToken ct)
    {
        _logger.LogInformation("Restart all processes requested");
        await _manager.RestartAllAsync(ct);
        return Ok(_manager.GetAllStatus());
    }
}
