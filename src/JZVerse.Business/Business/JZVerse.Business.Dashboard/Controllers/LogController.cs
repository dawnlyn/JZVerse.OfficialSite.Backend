using JZVerse.Business.Abstractions.Authentication;
using JZVerse.Business.Abstractions.Models;
using JZVerse.Business.Dashboard.Arguments;
using JZVerse.Business.Dashboard.Results;
using JZVerse.Business.Dashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JZVerse.Business.Dashboard.Controllers;

/// <summary>
/// 日志管理控制器
/// </summary>
[ApiController]
[Route("api/v1/dashboard/log")]
[Authorize(Roles = "admin")]
public sealed class LogController : ControllerBase
{
    private readonly LogService _logService;
    private readonly IUserContext _userContext;

    public LogController(LogService logService, IUserContext userContext)
    {
        _logService = logService;
        _userContext = userContext;
    }

    #region Operation Log

    /// <summary>
    /// 获取操作日志详情
    /// </summary>
    [HttpGet("operation/{id:guid}")]
    public async Task<ResultOperationLog?> GetOperationLogByIdAsync(Guid id)
    {
        return await _logService.GetOperationLogByIdAsync(id);
    }

    /// <summary>
    /// 查询操作日志列表
    /// </summary>
    [HttpGet("operation")]
    public async Task<PagedResult<ResultOperationLog>> GetOperationLogsAsync([FromQuery] ArgQueryOperationLogs arg)
    {
        return await _logService.GetOperationLogsAsync(arg);
    }

    /// <summary>
    /// 清理过期操作日志
    /// </summary>
    [HttpDelete("operation/cleanup")]
    public async Task<int> CleanupOperationLogsAsync([FromQuery] int daysToKeep = 90)
    {
        return await _logService.CleanupOperationLogsAsync(daysToKeep);
    }

    #endregion

    #region Exception Log

    /// <summary>
    /// 获取异常日志详情
    /// </summary>
    [HttpGet("exception/{id:guid}")]
    public async Task<ResultExceptionLog?> GetExceptionLogByIdAsync(Guid id)
    {
        return await _logService.GetExceptionLogByIdAsync(id);
    }

    /// <summary>
    /// 查询异常日志列表
    /// </summary>
    [HttpGet("exception")]
    public async Task<PagedResult<ResultExceptionLog>> GetExceptionLogsAsync([FromQuery] ArgQueryExceptionLogs arg)
    {
        return await _logService.GetExceptionLogsAsync(arg);
    }

    /// <summary>
    /// 处理异常日志
    /// </summary>
    [HttpPut("exception/handle")]
    public async Task<bool> HandleExceptionLogAsync([FromBody] ArgHandleExceptionLog arg)
    {
        return await _logService.HandleExceptionLogAsync(arg, _userContext.UserId);
    }

    #endregion

    #region Login Log

    /// <summary>
    /// 获取登录日志详情
    /// </summary>
    [HttpGet("login/{id:guid}")]
    public async Task<ResultLoginLog?> GetLoginLogByIdAsync(Guid id)
    {
        return await _logService.GetLoginLogByIdAsync(id);
    }

    /// <summary>
    /// 查询登录日志列表
    /// </summary>
    [HttpGet("login")]
    public async Task<PagedResult<ResultLoginLog>> GetLoginLogsAsync([FromQuery] ArgQueryLoginLogs arg)
    {
        return await _logService.GetLoginLogsAsync(arg);
    }

    /// <summary>
    /// 获取当前用户最近登录记录
    /// </summary>
    [HttpGet("login/recent")]
    public async Task<IReadOnlyList<ResultLoginLog>> GetRecentLoginsAsync([FromQuery] int limit = 10)
    {
        return await _logService.GetRecentLoginsByUserIdAsync(_userContext.UserId, limit);
    }

    #endregion
}
