using JZVerse.MicroHuaxia.Gateway.Abstractions;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Authentication;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;
using Microsoft.AspNetCore.Mvc;

namespace JZVerse.MicroHuaxia.Gateway.Server.Controllers;

/// <summary>
/// 网关管理控制器
/// </summary>
[ApiController]
[Route("api/v1/gateway")]
public class GatewayController : ControllerBase
{
    private readonly IRouteMatchingEngine _routeEngine;
    private readonly IRouteRepository _routeRepository;
    private readonly IAuthenticationStrategyRepository _authStrategyRepository;
    private readonly IAuditLogStore _auditLogStore;
    private readonly ILogger<GatewayController> _logger;

    public GatewayController(
        IRouteMatchingEngine routeEngine,
        IRouteRepository routeRepository,
        IAuthenticationStrategyRepository authStrategyRepository,
        IAuditLogStore auditLogStore,
        ILogger<GatewayController> logger)
    {
        _routeEngine = routeEngine;
        _routeRepository = routeRepository;
        _authStrategyRepository = authStrategyRepository;
        _auditLogStore = auditLogStore;
        _logger = logger;
    }

    // ==================== 路由管理 ====================

    /// <summary>
    /// 获取所有路由
    /// </summary>
    [HttpGet("routes")]
    [ProducesResponseType(typeof(IReadOnlyList<GatewayRoute>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoutes(CancellationToken cancellationToken)
    {
        var routes = await _routeRepository.GetAllAsync(cancellationToken);
        return Ok(routes);
    }

    /// <summary>
    /// 获取指定路由
    /// </summary>
    [HttpGet("routes/{routeId}")]
    [ProducesResponseType(typeof(GatewayRoute), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRoute(string routeId, CancellationToken cancellationToken)
    {
        var route = await _routeRepository.GetByIdAsync(routeId, cancellationToken);
        if (route is null)
        {
            return NotFound();
        }

        return Ok(route);
    }

    /// <summary>
    /// 添加路由
    /// </summary>
    [HttpPost("routes")]
    [ProducesResponseType(typeof(GatewayRoute), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddRoute([FromBody] GatewayRoute route, CancellationToken cancellationToken)
    {
        try
        {
            await _routeRepository.AddAsync(route, cancellationToken);
            _routeEngine.AddRoute(route);
            _logger.LogInformation("添加路由: {RouteId}", route.RouteId);
            return CreatedAtAction(nameof(GetRoute), new { routeId = route.RouteId }, route);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// 更新路由
    /// </summary>
    [HttpPut("routes/{routeId}")]
    [ProducesResponseType(typeof(GatewayRoute), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRoute(string routeId, [FromBody] GatewayRoute route, CancellationToken cancellationToken)
    {
        var existing = await _routeRepository.GetByIdAsync(routeId, cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        await _routeRepository.UpdateAsync(route, cancellationToken);
        _routeEngine.AddRoute(route); // AddRoute 会覆盖同 ID 的路由
        _logger.LogInformation("更新路由: {RouteId}", routeId);
        return Ok(route);
    }

    /// <summary>
    /// 删除路由
    /// </summary>
    [HttpDelete("routes/{routeId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRoute(string routeId, CancellationToken cancellationToken)
    {
        var result = await _routeRepository.DeleteAsync(routeId, cancellationToken);
        if (!result)
        {
            return NotFound();
        }

        _routeEngine.RemoveRoute(routeId);
        _logger.LogInformation("删除路由: {RouteId}", routeId);
        return NoContent();
    }

    /// <summary>
    /// 批量更新路由
    /// </summary>
    [HttpPost("routes/batch")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> BatchUpdateRoutes([FromBody] IEnumerable<GatewayRoute> routes, CancellationToken cancellationToken)
    {
        var routeList = routes.ToList();
        await _routeRepository.ReplaceAllAsync(routeList, cancellationToken);
        _routeEngine.UpdateRoutes(routeList);
        _logger.LogInformation("批量更新路由，共 {Count} 条", routeList.Count);
        return Ok(new { count = routeList.Count });
    }

    // ==================== 认证策略管理 ====================

    /// <summary>
    /// 获取所有认证策略
    /// </summary>
    [HttpGet("authentication/strategies")]
    [ProducesResponseType(typeof(IReadOnlyList<AuthenticationStrategy>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuthenticationStrategies(CancellationToken cancellationToken)
    {
        var strategies = await _authStrategyRepository.GetAllAsync(cancellationToken);
        return Ok(strategies);
    }

    /// <summary>
    /// 获取指定认证策略
    /// </summary>
    [HttpGet("authentication/strategies/{name}")]
    [ProducesResponseType(typeof(AuthenticationStrategy), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAuthenticationStrategy(string name, CancellationToken cancellationToken)
    {
        var strategy = await _authStrategyRepository.GetByNameAsync(name, cancellationToken);
        if (strategy is null)
        {
            return NotFound();
        }

        return Ok(strategy);
    }

    /// <summary>
    /// 添加认证策略
    /// </summary>
    [HttpPost("authentication/strategies")]
    [ProducesResponseType(typeof(AuthenticationStrategy), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddAuthenticationStrategy([FromBody] AuthenticationStrategy strategy, CancellationToken cancellationToken)
    {
        try
        {
            await _authStrategyRepository.AddAsync(strategy, cancellationToken);
            _logger.LogInformation("添加认证策略: {Name}", strategy.Name);
            return CreatedAtAction(nameof(GetAuthenticationStrategy), new { name = strategy.Name }, strategy);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// 删除认证策略
    /// </summary>
    [HttpDelete("authentication/strategies/{name}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAuthenticationStrategy(string name, CancellationToken cancellationToken)
    {
        var result = await _authStrategyRepository.DeleteAsync(name, cancellationToken);
        if (!result)
        {
            return NotFound();
        }

        _logger.LogInformation("删除认证策略: {Name}", name);
        return NoContent();
    }

    // ==================== 审计日志 ====================

    /// <summary>
    /// 查询审计日志
    /// </summary>
    [HttpPost("audit/query")]
    [ProducesResponseType(typeof(IReadOnlyList<AuditLogEntry>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryAuditLogs([FromBody] AuditLogQuery query, CancellationToken cancellationToken)
    {
        var logs = await _auditLogStore.QueryAsync(query, cancellationToken);
        return Ok(logs);
    }

    /// <summary>
    /// 获取审计日志统计
    /// </summary>
    [HttpGet("audit/statistics")]
    [ProducesResponseType(typeof(AuditLogStatistics), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditStatistics(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var fromTime = from ?? DateTimeOffset.UtcNow.AddHours(-24);
        var toTime = to ?? DateTimeOffset.UtcNow;
        var stats = await _auditLogStore.GetStatisticsAsync(fromTime, toTime, cancellationToken);
        return Ok(stats);
    }

    // ==================== 健康检查 ====================

    /// <summary>
    /// 网关健康检查
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTimeOffset.UtcNow,
            routes = _routeEngine.GetAllRoutes().Count
        });
    }
}
