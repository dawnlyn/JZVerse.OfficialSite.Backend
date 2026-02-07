using Microsoft.AspNetCore.Mvc;

namespace JZVerse.MicroHuaxia.ConfigCenter.Server.Controllers;

/// <summary>
/// 健康检查控制器
/// </summary>
[ApiController]
[Route("api/v1")]
public class HealthController : ControllerBase
{
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
            service = "config-center-server",
            timestamp = DateTimeOffset.UtcNow,
        });
    }

    /// <summary>
    /// 存活检查
    /// </summary>
    [HttpGet("alive")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Alive()
    {
        return Ok(new { status = "alive" });
    }
}
