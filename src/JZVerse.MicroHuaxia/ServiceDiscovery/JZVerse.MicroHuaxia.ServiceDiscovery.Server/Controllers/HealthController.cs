using Microsoft.AspNetCore.Mvc;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Server.Controllers;

/// <summary>
/// 注册中心健康检查控制器
/// </summary>
[ApiController]
[Route("api/v1")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// 注册中心自身的健康检查端点
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health() =>
        Ok(
            new
            {
                status = "healthy",
                service = "service-discovery-server",
                timestamp = DateTimeOffset.UtcNow,
            }
        );

    /// <summary>
    /// 存活检查端点
    /// </summary>
    [HttpGet("alive")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Alive() => Ok(new { status = "alive" });
}
