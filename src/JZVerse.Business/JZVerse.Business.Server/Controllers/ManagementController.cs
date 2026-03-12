using JZVerse.Business.Server.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JZVerse.Business.Server.Controllers;

/// <summary>
/// 服务管理 API 控制器
/// </summary>
[ApiController]
[Route("api/management")]
public sealed class ManagementController(
    ServiceRestartCoordinator _restartCoordinator,
    ILogger<ManagementController> _logger
) : ControllerBase
{
    /// <summary>
    /// 获取服务状态
    /// </summary>
    /// <returns>服务状态信息</returns>
    [HttpGet("status")]
    [ProducesResponseType<ServiceStatus>(StatusCodes.Status200OK)]
    public IActionResult GetStatus()
    {
        var status = _restartCoordinator.GetStatus();
        return Ok(status);
    }

    /// <summary>
    /// 触发服务重启
    /// </summary>
    /// <remarks>
    /// 此操作将触发服务的优雅重启：
    /// 1. 等待现有请求完成
    /// 2. 从服务发现注销
    /// 3. 停止应用
    /// 4. 由进程管理器重新启动
    /// </remarks>
    /// <returns>重启状态</returns>
    [HttpPost("restart")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Restart(CancellationToken cancellationToken)
    {
        _logger.LogInformation("收到服务重启请求，操作者: {User}", User.Identity?.Name ?? "Unknown");

        if (!_restartCoordinator.RestartRequired)
        {
            _logger.LogWarning("服务当前不需要重启");
            return BadRequest(new
            {
                Message = "服务当前不需要重启",
                RestartRequired = false
            });
        }

        try
        {
            // 异步触发重启，立即返回
            _ = _restartCoordinator.RestartAsync(cancellationToken);

            return Accepted(new
            {
                Message = "服务重启已触发",
                Status = "Restarting"
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// 强制重启服务（不检查是否需要重启）
    /// </summary>
    [HttpPost("restart/force")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ForceRestart(CancellationToken cancellationToken)
    {
        _logger.LogWarning("收到强制重启请求，操作者: {User}", User.Identity?.Name ?? "Unknown");

        try
        {
            // 强制标记需要重启
            _restartCoordinator.MarkRestartRequired(["ForceRestart"]);

            // 异步触发重启，立即返回
            _ = _restartCoordinator.RestartAsync(cancellationToken);

            return Accepted(new
            {
                Message = "服务强制重启已触发",
                Status = "Restarting"
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// 清除重启标记
    /// </summary>
    /// <remarks>
    /// 用于配置回滚等场景，清除重启标记后服务将不再显示需要重启状态
    /// </remarks>
    [HttpPost("restart/clear")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult ClearRestartFlag()
    {
        _logger.LogInformation("清除重启标记，操作者: {User}", User.Identity?.Name ?? "Unknown");

        _restartCoordinator.ClearRestartRequired();

        return Ok(new
        {
            Message = "重启标记已清除",
            RestartRequired = false
        });
    }
}
